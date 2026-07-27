using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Generation;
using Loom.Core.Modules;
using Loom.Core.Resolving;
using Loom.Core.Resolving.Symbols;
using Loom.Core.Text;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.Pipeline;

public sealed class CompilationUnit(LoomConfig config, DiagnosticOptions? diagnosticOptions = null)
{
    public LoomConfig Config { get; } = config;

    /// <summary>
    ///     Reporting behavior handed to every <see cref="DiagnosticBag" /> the unit's stages create. Defaults
    ///     to <see cref="DiagnosticOptions.Default" />, so a unit only fails fast when its creator asks for it.
    /// </summary>
    public DiagnosticOptions DiagnosticOptions { get; } = diagnosticOptions ?? DiagnosticOptions.Default;

    public List<SourceFile> SourceFiles { get; } = FileManager.LoadDirectory(config.Files.SourceDirectory);
    public Dictionary<Symbol, Type> Globals { get; } = [];
    public RuntimeImport RuntimeImport { get; } = RuntimeImport.Resolve(config);

    /// <summary>
    ///     Semantic models of files already analyzed in this unit, keyed by source file. Because analysis
    ///     follows the module graph's order, every module a file imports is present here by the time that
    ///     file is resolved, which is how the resolver reads a dependency's exports.
    /// </summary>
    public Dictionary<SourceFile, SemanticModel> AnalyzedModules { get; } = [];

    /// <summary>Names modules for the requires the generator emits, through the unit's Rojo project.</summary>
    public ModuleRequirePathResolver ModuleRequirePaths { get; } = new(config);

    /// <summary>
    ///     Import dependency graph of the unit, built between the two compilation phases. The resolver reads
    ///     it to find the module an import refers to, so it is null until <see cref="Compile()" /> runs.
    /// </summary>
    public ModuleGraph? ModuleGraph { get; private set; }

    public CompilationResult Compile()
    {
        Globals.Clear();
        AnalyzedModules.Clear();

        var failures = new List<FailedFile>();

        // phase one: every file is lexed and parsed before any of them is analyzed, so module
        // dependencies can be read off the parsed trees and analyzed in the order they require
        var parsedFiles = ParseAll(failures);
        var compilers = new Dictionary<SourceFile, Compiler>();
        foreach (var (compiler, parsedFile) in parsedFiles)
            compilers.TryAdd(parsedFile.File, compiler);

        ModuleGraph = ModuleGraph.Build(parsedFiles.ConvertAll(parsed => parsed.ParsedFile), Config, DiagnosticOptions);

        // phase two: declaration files first — their top-level symbols become globals that every
        // other file resolves against. Both groups keep the graph's dependency order.
        var compiledDeclarationFiles = AnalyzeAll(parsedFile => parsedFile.File.IsDeclaration);
        PopulateGlobals(compiledDeclarationFiles);

        var compiledConcreteFiles = AnalyzeAll(parsedFile => !parsedFile.File.IsDeclaration);
        var compiledFiles = compiledDeclarationFiles.Concat(compiledConcreteFiles).ToList();
        var diagnostics = DiagnosticBag.Concat(
            [..compiledFiles.ConvertAll(file => file.Diagnostics), ..failures.ConvertAll(failure => failure.Diagnostics)],
            DiagnosticOptions
        );

        if (!diagnostics.ContainsErrors() && !Config.NoEmit)
            compiledFiles.ForEach(FileManager.WriteCompiledFile);

        return new CompilationResult(compiledFiles, diagnostics) { Failures = failures };

        List<CompiledFile> AnalyzeAll(Predicate<ParsedFile> predicate)
        {
            var compiledFiles = new List<CompiledFile>();
            foreach (var parsedFile in ModuleGraph.Order.FindAll(predicate))
            {
                var compiledFile = compilers[parsedFile.File].Analyze(parsedFile, ModuleGraph.GetDiagnostics(parsedFile.File));
                if (compiledFile == null)
                {
                    // the file has no semantic model to hand its importers, so it stays out of the unit
                    failures.Add(new FailedFile(parsedFile.File, compilers[parsedFile.File].Diagnostics));
                    continue;
                }

                AnalyzedModules[parsedFile.File] = compiledFile.SemanticModel;
                compiledFiles.Add(compiledFile);
            }

            return compiledFiles;
        }
    }

    /// <summary>Null when the compiler gave up on <paramref name="file" />; see <see cref="Compiler.Diagnostics" />.</summary>
    public CompiledFile? Compile(SourceFile file) => new Compiler(this, file).Compile();

    /// <summary>
    ///     The compiler for a file is kept alongside its parsed form so that phase two reports the
    ///     lexer and parser diagnostics phase one collected.
    /// </summary>
    private List<(Compiler Compiler, ParsedFile ParsedFile)> ParseAll(List<FailedFile> failures)
    {
        var parsedFiles = new List<(Compiler, ParsedFile)>(SourceFiles.Count);
        foreach (var compiler in SourceFiles.Select(file => new Compiler(this, file)))
            if (compiler.Parse() is { } parsedFile)
                parsedFiles.Add((compiler, parsedFile));
            else
                failures.Add(new FailedFile(compiler.SourceFile, compiler.Diagnostics));

        return parsedFiles;
    }

    private void PopulateGlobals(List<CompiledFile> compiledDeclarationFiles)
    {
        foreach (var compiledFile in compiledDeclarationFiles)
        {
            foreach (var symbol in compiledFile.Tree.Statements.Select(statement => compiledFile.SemanticModel.GetDeclarationSymbol(statement)).OfType<Symbol>())
            {
                var type = compiledFile.SemanticModel.GetType(symbol.Declaration);
                Globals.Add(symbol, type);
            }
        }
    }
}