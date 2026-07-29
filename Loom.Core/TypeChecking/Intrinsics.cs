using System.Collections.Concurrent;
using Loom.Config;
using Loom.Core.Pipeline;
using Loom.Core.Resolving;
using Loom.Core.Resolving.Symbols;
using Loom.Core.Text;
using Loom.Core.TypeChecking.Types;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.TypeChecking;

public static class Intrinsics
{
    private const string CoreFileName = "loom.loom";
    private const string PluginSecurityFileName = "PluginSecurity.loom";
    private const string NonPluginRuntimeFileName = "None.loom";

    // Compiling the intrinsic files below sends each one through the same Resolver pipeline as any
    // regular source file, which unconditionally calls back into Register(). This flag breaks that
    // recursion: the reentrant call sees no cached entry and returns immediately (see CompileIntrinsics),
    // which is fine because intrinsic files reach each other through compilationUnit.Globals instead of
    // ambient injection - see CompileCoreFile. Thread-local so one thread bootstrapping doesn't also
    // suppress a genuine, unrelated Register() call happening concurrently on another thread.
    [ThreadStatic]
    private static bool _isBootstrapping;

    // Keyed by ProjectType because PluginSecurity.loom and None.loom are only included for some project
    // types (see IsIncludedFor) - a single unkeyed cache would serve one project type's intrinsics to
    // every other project type compiled afterward in the same process.
    private static readonly ConcurrentDictionary<ProjectType, HashSet<(Symbol, Type)>> _cache = new();

    public static readonly TupleMarkerType TupleMarker = new();

    public static readonly InterfaceType Range = new(
        "Range",
        [],
        new ObjectType(
            null,
            [
                new ObjectProperty(false, "minimum", PrimitiveType.Number),
                new ObjectProperty(false, "maximum", PrimitiveType.Number),
                new ObjectProperty(false, "length", PrimitiveType.Number),
                new ObjectProperty(false, "clamp", new FunctionType([], [PrimitiveType.Number], PrimitiveType.Number))
            ]
        )
    );

    public static readonly InterfaceType StringMembers = new(
        "string",
        [],
        new ObjectType(
            null,
            [
                new ObjectProperty(false, "length", PrimitiveType.Number),
                new ObjectProperty(false, "upper", new FunctionType([], [], PrimitiveType.String)),
                new ObjectProperty(false, "lower", new FunctionType([], [], PrimitiveType.String)),
                new ObjectProperty(false, "trim", new FunctionType([], [], PrimitiveType.String)),
                new ObjectProperty(false, "replace", new FunctionType([], [PrimitiveType.String, PrimitiveType.String], PrimitiveType.String)),
                new ObjectProperty(false, "reverse", new FunctionType([], [], PrimitiveType.String)),
                new ObjectProperty(false, "repeat", new FunctionType([], [PrimitiveType.Number], PrimitiveType.String)),
                new ObjectProperty(false, "split", new FunctionType([], [new OptionalType(PrimitiveType.String)], new ArrayType(PrimitiveType.String, true))),
                new ObjectProperty(false, "has", new FunctionType([], [PrimitiveType.String], PrimitiveType.Bool)),
                new ObjectProperty(false, "starts_with", new FunctionType([], [PrimitiveType.String], PrimitiveType.Bool)),
                new ObjectProperty(false, "ends_with", new FunctionType([], [PrimitiveType.String], PrimitiveType.Bool)),
                new ObjectProperty(false, "byte", new FunctionType([], [new OptionalType(PrimitiveType.Number)], new OptionalType(PrimitiveType.Number)))
            ]
        )
    );

    public static HashSet<(Symbol, Type)> Register(SemanticModel model, CompilationUnit injectInto)
    {
        var intrinsics = _cache.GetOrAdd(injectInto.Config.ProjectType, CompileIntrinsics);
        foreach (var (symbol, type) in intrinsics)
            model.TypeSolver.SetType(symbol.Declaration, type);

        return intrinsics;
    }

    private static HashSet<(Symbol, Type)> CompileIntrinsics(ProjectType projectType)
    {
        if (_isBootstrapping)
            return [];

        _isBootstrapping = true;
        try
        {
            var compilationUnit = CreateCompilationUnit();
            var files = compilationUnit.SourceFiles.Where(file => IsIncludedFor(file, projectType)).ToList();
            foreach (var file in files)
                file.IsIntrinsic = true;

            var coreFile = files.Find(file => file.Name == CoreFileName);
            var compiledFiles = new List<CompiledFile>();
            if (coreFile != null && CompileCoreFile(compilationUnit, coreFile) is { } compiledCoreFile)
                compiledFiles.Add(compiledCoreFile);

            foreach (var file in files)
                if (file != coreFile && compilationUnit.Compile(file) is { } compiledFile)
                    compiledFiles.Add(compiledFile);

            return CollectDeclaredSymbols(compiledFiles);
        }
        finally
        {
            _isBootstrapping = false;
        }
    }

    private static CompilationUnit CreateCompilationUnit()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var config = new LoomConfig
        {
            ProjectType = ProjectType.Library,
            NoEmit = true,
            Files = new FilesConfig { SourceDirectory = $"{repositoryRoot}/Loom.Core/TypeChecking/Intrinsic" }
        };

        return new CompilationUnit(config);
    }

    private static bool IsIncludedFor(SourceFile file, ProjectType projectType) =>
        file.Name switch
        {
            PluginSecurityFileName => projectType == ProjectType.Plugin,
            NonPluginRuntimeFileName => projectType != ProjectType.Plugin,
            _ => true
        };

    /// <summary>
    ///     loom.loom declares luau_name, luau_method, and override, which every other intrinsic file's
    ///     attributes depend on, so it must compile - and have its declarations copied into
    ///     <see cref="CompilationUnit.Globals" /> - before any other intrinsic file does. Globals is the
    ///     same channel a regular project's own .d.loom files use to reach every other file in the unit;
    ///     it is the only channel intrinsic files have for referencing each other, since ambient intrinsic
    ///     injection stays off for this whole bootstrap (see <see cref="_isBootstrapping" />).
    /// </summary>
    private static CompiledFile? CompileCoreFile(CompilationUnit compilationUnit, SourceFile coreFile)
    {
        var compiled = compilationUnit.Compile(coreFile);
        if (compiled == null)
            return null;

        foreach (var symbol in compiled.Tree.Statements.SelectMany(statement => compiled.SemanticModel.GetDeclarationSymbols(statement)))
            compilationUnit.Globals[symbol] = compiled.SemanticModel.GetType(symbol.Declaration);

        return compiled;
    }

    private static HashSet<(Symbol, Type)> CollectDeclaredSymbols(IEnumerable<CompiledFile> compiledFiles)
    {
        var intrinsicSymbols = new HashSet<(Symbol, Type)>();
        foreach (var compiledFile in compiledFiles)
        foreach (var symbol in compiledFile.Tree.Statements.SelectMany(statement => compiledFile.SemanticModel.GetDeclarationSymbols(statement)))
        {
            symbol.IsIntrinsic = true;
            symbol.IsGlobal = true;
            intrinsicSymbols.Add((symbol, compiledFile.SemanticModel.GetType(symbol.Declaration)));
        }

        return intrinsicSymbols;
    }
}