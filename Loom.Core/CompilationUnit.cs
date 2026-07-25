using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Resolving;
using Loom.Core.Text;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core;

public sealed class CompilationUnit(LoomConfig config)
{
    public LoomConfig Config { get; } = config;
    public List<SourceFile> SourceFiles { get; } = FileManager.LoadDirectory(config.Files.SourceDirectory);
    public Dictionary<Symbol, Type> Globals { get; } = [];
    public RuntimeImport RuntimeImport { get; } = ResolveRuntimeImport(config);

    private static RuntimeImport ResolveRuntimeImport(LoomConfig config)
    {
        var resolver = RojoResolver.FromProjectDirectory(config.ProjectDirectory);
        if (resolver == null)
            return RuntimeImport.Default;

        var segments = resolver.ResolveRuntimePath();
        return segments == null
            ? new RuntimeImport(RuntimeImportStatus.NotFoundInRojo, Core.RuntimeImport.DefaultPath)
            : new RuntimeImport(RuntimeImportStatus.Resolved, RuntimeImport.PathPrefix + string.Join('/', segments));
    }
    
    public CompilationResult Compile()
    {
        Globals.Clear();

        // phase one: every file is lexed and parsed before any of them is analyzed, so module
        // dependencies can be read off the parsed trees and analyzed in the order they require
        var parsedFiles = ParseAll();

        // phase two: declaration files first — their top-level symbols become globals that every
        // other file resolves against
        var compiledDeclarationFiles = parsedFiles.FindAll(parsed => parsed.ParsedFile.File.IsDeclaration).ConvertAll(Analyze);
        PopulateGlobals(compiledDeclarationFiles);

        var compiledConcreteFiles = parsedFiles.FindAll(parsed => !parsed.ParsedFile.File.IsDeclaration).ConvertAll(Analyze);
        var compiledFiles = compiledDeclarationFiles.Concat(compiledConcreteFiles).ToList();
        var diagnostics = DiagnosticBag.Concat(compiledFiles.ConvertAll(file => file.Diagnostics));
        if (!diagnostics.ContainsErrors() && !Config.NoEmit)
            compiledFiles.ForEach(FileManager.WriteCompiledFile);

        return new CompilationResult(compiledFiles, diagnostics);
    }

    public CompiledFile Compile(SourceFile file) => new Compiler(this, file).Compile();

    /// <summary>
    /// The compiler for a file is kept alongside its parsed form so that phase two reports the
    /// lexer and parser diagnostics phase one collected.
    /// </summary>
    private List<(Compiler Compiler, ParsedFile ParsedFile)> ParseAll()
    {
        var parsedFiles = new List<(Compiler, ParsedFile)>(SourceFiles.Count);
        foreach (var file in SourceFiles)
        {
            var compiler = new Compiler(this, file);
            if (compiler.Parse() is { } parsedFile)
                parsedFiles.Add((compiler, parsedFile));
        }

        return parsedFiles;
    }

    private static CompiledFile Analyze((Compiler Compiler, ParsedFile ParsedFile) parsed) => parsed.Compiler.Analyze(parsed.ParsedFile);

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