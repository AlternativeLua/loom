using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Modules;
using Loom.Core.Pipeline;
using Loom.Core.Text;

namespace Loom.Testing;

/// <summary>
///     A unit spanning two projects: the entry app, and a package it depends on, distributed as source. The
///     two roots disagree on project directory, output directory and identity, so every decision the compiler
///     makes per file has to follow that file's own root rather than the entry project's.
/// </summary>
[Collection("Assembly")]
public class SourceRootTest
{
    private const string AppManifest = "project_type = \"game\"\n[dependencies]\nmath = \"^1.0\"\n";

    /// <summary>An app that compiles the package without depending on it, as it would a dependency of a dependency.</summary>
    private const string AppManifestWithoutDependencies = "project_type = \"game\"\n";

    private const string PackageManifest = "project_type = \"library\"\n[package]\nname = \"math\"\nversion = \"1.0.0\"\n";

    /// <summary>Maps both projects' output directories into the tree, each under a name of its own.</summary>
    private const string AppRojoProject = """
        {
          "tree": {
            "$className": "DataModel",
            "ReplicatedStorage": {
              "Shared": { "$path": "dist" },
              "Packages": { "$path": "../packages/math/dist" }
            }
          }
        }
        """;

    private sealed record Workspace(string Directory, LoomConfig App, LoomConfig Package);

    [Fact]
    public void Owns_TheFilesUnderItsOwnSourceDirectory()
        => WithWorkspace((workspace, unit) =>
            {
                var main = unit.SourceFiles.First(file => file.Name == "main.loom");
                var package = unit.SourceFiles.First(file => file.Name == "init.loom");

                Assert.Equal(2, unit.SourceFiles.Count());
                Assert.Same(unit.Roots.Entry, unit.Roots.Of(main));
                Assert.Same(unit.Roots[1], unit.Roots.Of(package));
                Assert.Same(workspace.Package, unit.Roots.ConfigOf(package));

                // a file under no root at all - an intrinsic, a lone file handed to the unit - reads the
                // entry project's settings, which is what it read when a unit had a single root
                Assert.Same(unit.Roots.Entry, unit.Roots.Of(Utility.TestFile("let x = 1;")));
            }
        );

    [Fact]
    public void Compiles_EveryRoot_IntoItsOwnOutputDirectory()
        => WithWorkspace((workspace, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertNoErrors(result);
                Assert.Equal(2, result.Files.Count);

                var main = result.Files.Single(file => file.SourceFile.Name == "main.loom");
                var package = result.Files.Single(file => file.SourceFile.Name == "init.loom");

                Assert.Equal(Path.Combine(workspace.App.Files.OutputDirectory, "main.luau"), main.Path);
                Assert.Equal(Path.Combine(workspace.Package.Files.OutputDirectory, "init.luau"), package.Path);
                Assert.True(File.Exists(main.Path));
                Assert.True(File.Exists(package.Path));
            }
        );

    [Fact]
    public void Emits_OnlyTheRoots_WhoseOwnConfigAsksForOutput()
        => WithWorkspace((workspace, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertNoErrors(result);

                Assert.True(File.Exists(Path.Combine(workspace.App.Files.OutputDirectory, "main.luau")));
                Assert.False(Directory.Exists(workspace.Package.Files.OutputDirectory));
            },
            configure: workspace => workspace.Package.NoEmit = true
        );

    /// <remarks>
    ///     Reaching out of one project and into another is what a package specifier is for; a relative
    ///     specifier that climbs past its own root would bind to a module the consumer cannot require.
    /// </remarks>
    [Fact]
    public void Rejects_ARelativeImport_ThatLeavesItsOwnRoot()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertDiagnostic(
                    result.Diagnostics,
                    InternalCodes.ModuleOutsideSourceDirectory,
                    "Module '../../packages/math/src/init' is outside the source directory."
                );
            },
            appFiles: [("main.loom", "import { pi } from \"../../packages/math/src/init\"\nprint(pi);")]
        );

    /// <remarks>
    ///     The entry project's Rojo tree names every module of the unit, dependencies included: it is the one
    ///     describing the place the compiled game runs in. What differs per root is the output path being
    ///     looked up, since that is where the root wrote its Luau.
    /// </remarks>
    [Fact]
    public void Names_ADependencyModule_ThroughTheEntryProjectsRojoTree()
        => WithWorkspace((_, unit) =>
            {
                var main = unit.SourceFiles.First(file => file.Name == "main.loom");
                var package = unit.SourceFiles.First(file => file.Name == "init.loom");

                Assert.Equal(
                    new ModuleRequirePath(ModuleRequirePathStatus.Resolved, "@game/ReplicatedStorage/Shared/main"),
                    unit.ModuleRequirePaths.Resolve(main, "./main")
                );

                Assert.Equal(
                    new ModuleRequirePath(ModuleRequirePathStatus.Resolved, "@game/ReplicatedStorage/Packages"),
                    unit.ModuleRequirePaths.Resolve(package, "math")
                );
            },
            rojoProject: AppRojoProject
        );

    /// <remarks>
    ///     A bare specifier names the package's entry module — the <c>init.loom</c> at the top of its source
    ///     directory — the way a relative specifier names the <c>init.loom</c> of a folder.
    /// </remarks>
    [Fact]
    public void Resolves_ABareSpecifier_ToTheDependencysEntryModule()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertNoErrors(result);

                Assert.Equal("init.loom", ResolvedModuleOf(unit, "main.loom")?.Name);
            },
            appFiles: [("main.loom", "import { pi } from \"math\"\nprint(pi);")]
        );

    [Fact]
    public void Resolves_ABareSpecifier_WithASubpath_ToAModuleInsideTheDependency()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertNoErrors(result);

                Assert.Equal("vector.loom", ResolvedModuleOf(unit, "main.loom")?.Name);
            },
            appFiles: [("main.loom", "import { zero } from \"math/vector\"\nprint(zero);")],
            packageFiles: [("init.loom", "export let pi = 3;"), ("vector.loom", "export let zero = 0;")]
        );

    /// <remarks>A package refers to its own modules by its own name too, without depending on itself to do it.</remarks>
    [Fact]
    public void Resolves_ABareSpecifier_NamingThePackageItIsWrittenIn()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertNoErrors(result);

                Assert.Equal("vector.loom", ResolvedModuleOf(unit, "init.loom")?.Name);
            },
            packageFiles: [("init.loom", "import { zero } from \"math/vector\"\nexport let pi = zero;"), ("vector.loom", "export let zero = 0;")]
        );

    /// <remarks>
    ///     Everything the build compiles is reachable by name, so a package pulled in only because something
    ///     else depends on it would otherwise be importable by a project that never asked for it — and would
    ///     vanish the day that other project stopped depending on it.
    /// </remarks>
    [Fact]
    public void Rejects_AnImport_OfAPackage_TheProjectDoesNotDependOn()
        => WithWorkspace((_, unit) => Utility.AssertDiagnostic(
                unit.Compile().Diagnostics,
                InternalCodes.UndeclaredDependency,
                "Package 'math' is not a dependency of this project.",
                "it is only in this build because something else depends on it; add 'math' to [dependencies] to import it yourself"
            ),
            appFiles: [("main.loom", "import { pi } from \"math\"\nprint(pi);")],
            appManifest: AppManifestWithoutDependencies
        );

    [Fact]
    public void Rejects_APackageSubpath_ThatClimbsOutOfThatPackage()
        => WithWorkspace((_, unit) => Utility.AssertDiagnostic(
                unit.Compile().Diagnostics,
                InternalCodes.ModuleOutsideSourceDirectory,
                "Module 'math/../../../app/src/main' is outside the source directory."
            ),
            appFiles: [("main.loom", "import { pi } from \"math/../../../app/src/main\"\nprint(pi);")]
        );

    /// <remarks>
    ///     No relative path reaches into another root, so the module a casing mistake meant has to be named the
    ///     only way the importing file could have written it: by its package.
    /// </remarks>
    [Fact]
    public void Names_ADependencysModule_ByItsPackage_WhenHintingAtACasingMistake()
        => WithWorkspace((_, unit) => Utility.AssertDiagnostic(
                unit.Compile().Diagnostics,
                InternalCodes.ModuleNotFound,
                "Could not find module 'math/Vector'.",
                "did you mean 'math/vector'? module paths are case-sensitive"
            ),
            appFiles: [("main.loom", "import { zero } from \"math/Vector\"\nprint(zero);")],
            packageFiles: [("init.loom", "export let pi = 3;"), ("vector.loom", "export let zero = 0;")]
        );

    /// <remarks>
    ///     A package's public surface is what it exports: exports are versioned, named at the import site and
    ///     shadowable, none of which is true of a name that simply turns up in scope. Its declaration files
    ///     furnish the package itself and stop there.
    /// </remarks>
    [Fact]
    public void Keeps_ADependencysAmbientDeclarations_OutOfTheConsumersScope()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertDiagnostic(result.Diagnostics, InternalCodes.CannotFindName, "Cannot find name 'physics_step'.");

                // the package's own files still see it, and no name of the package's leaked into the app
                var main = unit.SourceFiles.First(file => file.Name == "main.loom");
                var package = unit.SourceFiles.First(file => file.Name == "init.loom");

                Assert.Contains(unit.Globals.Of(package).Keys, symbol => symbol.Name == "physics_step");
                Assert.Empty(unit.Globals.Of(main));
            },
            appFiles: [("main.loom", "print(physics_step);")],
            packageFiles: [("init.loom", "export let pi = physics_step;"), ("globals.d.loom", "declare let physics_step: number;")]
        );

    /// <remarks>Each root's ambient scope is its own, so the same name in two of them is two declarations, not a collision.</remarks>
    [Fact]
    public void Compiles_TwoRoots_DeclaringTheSameAmbientName()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                Utility.AssertNoErrors(result);

                var main = unit.SourceFiles.First(file => file.Name == "main.loom");
                var package = unit.SourceFiles.First(file => file.Name == "init.loom");

                var appGlobal = Assert.Single(unit.Globals.Of(main).Keys, symbol => symbol.Name == "version");
                var packageGlobal = Assert.Single(unit.Globals.Of(package).Keys, symbol => symbol.Name == "version");

                Assert.NotSame(appGlobal, packageGlobal);
                Assert.Equal("globals.d.loom", appGlobal.File.Name);
                Assert.Equal("package-globals.d.loom", packageGlobal.File.Name);
            },
            appFiles: [("main.loom", "print(version);"), ("globals.d.loom", "declare let version: string;")],
            packageFiles: [("init.loom", "export let pi = version;"), ("package-globals.d.loom", "declare let version: number;")]
        );

    /// <remarks>Intrinsics belong to the language rather than to a project, so partitioning globals by root does not reach them.</remarks>
    [Fact]
    public void Resolves_Intrinsics_FromEveryRoot()
        => WithWorkspace((_, unit) => Utility.AssertNoErrors(unit.Compile()),
            appFiles: [("main.loom", "print(\"app\");")],
            packageFiles: [("init.loom", "export let pi = 3;\nprint(\"package\");")]
        );

    [Fact]
    public void Names_ADependencysFiles_ByItsPackage_WhenReportingACycle()
        => WithWorkspace((_, unit) =>
            {
                var result = unit.Compile();
                var cycle = result.Diagnostics.Find(diagnostic => diagnostic.Code == InternalCodes.CircularModuleDependency);

                Assert.NotNull(cycle);
                Assert.Contains("math/init.loom", cycle.Message);
                Assert.Contains("math/util.loom", cycle.Message);
            },
            packageFiles:
            [
                ("init.loom", "import { helper } from \"./util\"\nexport let pi = helper;"),
                ("util.loom", "import { pi } from \"./init\"\nexport let helper = pi;")
            ]
        );

    /// <remarks>
    ///     A vendored package sits under the source directory of the project depending on it, so both roots
    ///     load its files. The innermost root owns them, and no file is left compiled twice into two places.
    /// </remarks>
    [Fact]
    public void Owns_AVendoredPackage_OverTheProjectItSitsInside()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "loom-test-" + Guid.NewGuid());
        try
        {
            var app = WriteProject(Path.Combine(workspace, "app"), AppManifest, [("main.loom", "let x = 1;")]);
            var package = WriteProject(Path.Combine(workspace, "app", "src", "packages", "math"), PackageManifest, [("init.loom", "export let pi = 3;")]);
            package.NoEmit = app.NoEmit = true;

            var unit = new CompilationUnit(new SourceRootSet(new SourceRoot(app), new SourceRoot(package)));
            var vendored = Assert.Single(unit.SourceFiles, file => file.Name == "init.loom");

            Assert.Same(unit.Roots[1], unit.Roots.Of(vendored));
            Assert.DoesNotContain(unit.Roots.Entry.Files, file => file.Name == "init.loom");

            var result = unit.Compile();
            Utility.AssertNoErrors(result);
            Assert.Equal(2, result.Files.Count);
            Assert.Equal(Path.Combine(package.Files.OutputDirectory, "init.luau"), result.Files.Single(file => file.SourceFile.Name == "init.loom").Path);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    /// <summary>The module the single import of <paramref name="fileName" /> resolved to.</summary>
    private static SourceFile? ResolvedModuleOf(CompilationUnit unit, string fileName)
    {
        var graph = Assert.IsType<ModuleGraph>(unit.ModuleGraph);
        var file = graph.Order.First(parsed => parsed.File.Name == fileName);

        return graph.GetResolvedModule(Assert.Single(file.Imports));
    }

    /// <summary>
    ///     Runs <paramref name="assert" /> against a unit spanning a throwaway workspace's two projects: the
    ///     entry app in <c>app/</c>, and the <c>math</c> package it depends on in <c>packages/math/</c>.
    /// </summary>
    private static void WithWorkspace(
        Action<Workspace, CompilationUnit> assert,
        IEnumerable<(string Path, string Source)>? appFiles = null,
        IEnumerable<(string Path, string Source)>? packageFiles = null,
        string? rojoProject = null,
        string? appManifest = null,
        Action<Workspace>? configure = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-test-" + Guid.NewGuid());
        try
        {
            var appDirectory = Path.Combine(directory, "app");
            var app = WriteProject(appDirectory, appManifest ?? AppManifest, appFiles ?? [("main.loom", "let x = 1;")]);
            var package = WriteProject(
                Path.Combine(directory, "packages", "math"),
                PackageManifest,
                packageFiles ?? [("init.loom", "export let pi = 3;")]
            );

            if (rojoProject != null)
                File.WriteAllText(Path.Combine(appDirectory, RojoResolver.ProjectFileName), rojoProject);

            var workspace = new Workspace(directory, app, package);
            configure?.Invoke(workspace);

            assert(workspace, new CompilationUnit(new SourceRootSet(new SourceRoot(app), new SourceRoot(package))));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Writes a project directory - its manifest and its source files - and returns the config located from it.</summary>
    private static LoomConfig WriteProject(string directory, string manifest, IEnumerable<(string Path, string Source)> files)
    {
        var sourceDirectory = Path.Combine(directory, "src");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(
            Path.Combine(directory, "loom-config.toml"),
            manifest + "[files]\nsource_directory = \"src\"\noutput_directory = \"dist\"\n"
        );

        foreach (var (path, source) in files)
        {
            var filePath = Path.Combine(sourceDirectory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, source);
        }

        var config = ConfigReader.LocateFromDirectory(directory);
        Assert.NotNull(config);

        return config;
    }
}
