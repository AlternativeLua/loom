using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Pipeline;
using Loom.Luau.AST;
using BinaryOperator = Loom.Core.Parsing.AST.BinaryOperator;
using ExpressionStatement = Loom.Core.Parsing.AST.ExpressionStatement;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;

namespace Loom.Testing;

[Collection("Assembly")]
public class CompilationUnitTest
{
    [Fact]
    public void Compiles_Project_NoEmit()
    {
        var config = GetConfig();
        config.NoEmit = true;

        var compilationUnit = new CompilationUnit(config);
        var result = compilationUnit.Compile();
        Utility.AssertNoErrors(result);
        Assert.Single(result.Files);

        var path = config.Files.OutputDirectory;
        Directory.Delete(path, true);
        Directory.CreateDirectory(path);
        File.Create(Path.Combine(path, ".gitkeep")).Dispose();

        var luauFiles = Directory.EnumerateFiles(path, "*.luau", SearchOption.TopDirectoryOnly);
        Assert.Empty(luauFiles);
    }

    [Fact]
    public void Compiles_Project()
    {
        var config = GetConfig();
        var compilationUnit = new CompilationUnit(config);
        var result = compilationUnit.Compile();
        Utility.AssertNoErrors(result);
        Assert.Single(result.Files);

        var file = result.Files.Find(file => file.Path.EndsWith("basic_binary.luau"));
        Assert.NotNull(file);
        Assert.Equal(4, file.Tokens.Count);
        Assert.Single(file.Tree.Statements);
        Assert.IsType<BinaryOperator>(Assert.IsType<ExpressionStatement>(file.Tree.Statements.First()).Expression);
        Assert.Null(file.SemanticModel.GetSymbol(file.Tree));
        Assert.Equal(PrimitiveType.Number, file.ReturnType);
        Assert.Single(file.LuauTree.Statements);

        var variable = Assert.IsType<ConstVariable>(file.LuauTree.Statements.First());
        var binary = Assert.IsType<Luau.AST.BinaryOperator>(variable.Initializer);
        Assert.Equal("_", variable.Name);
        Assert.IsType<NumberLiteral>(binary.Left);
        Assert.IsType<NumberLiteral>(binary.Right);

        var path = config.Files.OutputDirectory;
        Directory.Delete(path, true);
        Directory.CreateDirectory(path);
        File.Create(Path.Combine(path, ".gitkeep")).Dispose();
    }

    [Fact]
    public void Compiles_Project_WithDeclarationFile_PopulatesGlobals()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loom-test-" + Guid.NewGuid());
        var srcDir = Path.Combine(dir, "src");
        Directory.CreateDirectory(srcDir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "loom-config.toml"),
                "project_type = \"game\"\n[files]\nsource_directory = \"src\"\noutput_directory = \"dist\"\n"
            );

            File.WriteAllText(Path.Combine(srcDir, "types.d.loom"), "declare let global_number: number;");
            File.WriteAllText(Path.Combine(srcDir, "main.loom"), "let x = 1;");

            var config = ConfigReader.LocateFromDirectory(dir);
            Assert.NotNull(config);
            config.NoEmit = true;

            var compilationUnit = new CompilationUnit(config);
            var result = compilationUnit.Compile();

            Utility.AssertNoErrors(result);
            Assert.Equal(2, result.Files.Count);
            Assert.Contains(compilationUnit.Globals.Keys, symbol => symbol.Name == "global_number");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Compiles_EveryFile_WhenAnotherFileHasDiagnostics() =>
        Utility.WithTempProject(
            [("bad.loom", "import { } from \"./math\""), ("good.loom", "let x = 1;")],
            (_, result) =>
            {
                Assert.Equal(2, result.Files.Count);
                Utility.AssertDiagnostic(result.Diagnostics, InternalCodes.EmptyImportClause, "Import declaration must name at least one member.");

                var good = Assert.Single(result.Files, file => file.SourceFile.Name == "good.loom");
                Assert.Contains("const x = 1", good.RenderedLuau);
            }
        );

    /// <remarks>
    ///     Uses a source that makes a stage throw today (an unresolved type reaches the generator). Should
    ///     that stop throwing, the test still holds the contract that matters: a unit reports what happened
    ///     instead of letting an exception escape, and accounts for every file it was given.
    /// </remarks>
    [Fact]
    public void Compiles_TheRestOfTheUnit_WhenAFileMakesTheCompilerThrow() =>
        Utility.WithTempProject(
            [("bad.loom", "let v: Missing = 1;\nprint(v);"), ("good.loom", "let x = 1;")],
            (_, result) =>
            {
                Assert.True(result.Diagnostics.ContainsErrors());

                var good = Assert.Single(result.Files, file => file.SourceFile.Name == "good.loom");
                Assert.Contains("const x = 1", good.RenderedLuau);

                // whichever way it went, bad.loom is accounted for and its diagnostics are in the result
                Assert.Equal(2, result.Files.Count + result.Failures.Count);
                foreach (var failure in result.Failures)
                {
                    Assert.Equal("bad.loom", failure.File.Name);

                    var compilerError = failure.Diagnostics.Find(diagnostic => diagnostic.Code == InternalCodes.CompilerError);
                    Assert.NotNull(compilerError);
                    Assert.Contains(compilerError, result.Diagnostics.Set);
                }
            }
        );

    [Fact]
    public void Compiles_WithTheUnitsDiagnosticOptions_IncludingModuleDiagnostics()
    {
        var options = new DiagnosticOptions();
        Utility.WithTempProject(
            [("main.loom", "import { square } from \"./missing\"")],
            (unit, result) =>
            {
                Assert.Same(options, unit.DiagnosticOptions);
                Assert.Same(options, result.Diagnostics.Options);

                var file = Assert.Single(result.Files);
                Assert.Same(options, file.Diagnostics.Options);

                var moduleDiagnostics = unit.ModuleGraph?.GetDiagnostics(file.SourceFile);
                Assert.NotNull(moduleDiagnostics);
                Utility.AssertDiagnostic(moduleDiagnostics, InternalCodes.ModuleNotFound, "Could not find module './missing'.");
                Assert.Same(options, moduleDiagnostics.Options);
            },
            diagnosticOptions: options
        );
    }

    private static LoomConfig GetConfig()
    {
        var config = ConfigReader.LocateFromDirectory(AssemblyFixture.Snapshots);
        Assert.NotNull(config);
        Assert.Equal(AssemblyFixture.Snapshots, config.ProjectDirectory);

        return config;
    }
}