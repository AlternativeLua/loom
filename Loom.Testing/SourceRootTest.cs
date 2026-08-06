using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Modules;
using Loom.Core.Pipeline;

namespace Loom.Testing;

/// <summary>
///     A unit spanning two projects: the entry app, and a package it depends on, distributed as source. The
///     two roots disagree on project directory, output directory and identity, so every decision the compiler
///     makes per file has to follow that file's own root rather than the entry project's.
/// </summary>
[Collection("Assembly")]
public class SourceRootTest
{
    private const string AppManifest = "project_type = \"game\"\n";
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

    /// <summary>
    ///     Runs <paramref name="assert" /> against a unit spanning a throwaway workspace's two projects: the
    ///     entry app in <c>app/</c>, and the <c>math</c> package it depends on in <c>packages/math/</c>.
    /// </summary>
    private static void WithWorkspace(
        Action<Workspace, CompilationUnit> assert,
        IEnumerable<(string Path, string Source)>? appFiles = null,
        IEnumerable<(string Path, string Source)>? packageFiles = null,
        string? rojoProject = null,
        Action<Workspace>? configure = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-test-" + Guid.NewGuid());
        try
        {
            var appDirectory = Path.Combine(directory, "app");
            var app = WriteProject(appDirectory, AppManifest, appFiles ?? [("main.loom", "let x = 1;")]);
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
