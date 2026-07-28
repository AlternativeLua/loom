using Loom.Config;
using Loom.Core.Pipeline;
using Loom.Core.Resolving;
using Loom.Core.Resolving.Symbols;
using Loom.Core.TypeChecking.Types;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.TypeChecking;

public static class Intrinsics
{
    private static HashSet<(Symbol, Type)>? _cachedIntrinsics;
    private static bool _compilingIntrinsic;

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

    public static HashSet<(Symbol, Type)> Register(SemanticModel model, CompilationUnit injectInto)
    {
        _cachedIntrinsics ??= CompileIntrinsics(injectInto);

        foreach (var (symbol, type) in _cachedIntrinsics)
            model.TypeSolver.SetType(symbol.Declaration, type);

        return _cachedIntrinsics;
    }

    private static HashSet<(Symbol, Type)> CompileIntrinsics(CompilationUnit injectInto)
    {
        if (_compilingIntrinsic) return [];
        _compilingIntrinsic = true;

        var sourceDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var loomConfig = new LoomConfig
        {
            ProjectType = ProjectType.Library, NoEmit = true, Files = new FilesConfig { SourceDirectory = $"{sourceDirectory}/Loom.Core/TypeChecking/Intrinsic" }
        };

        var compilationUnit = new CompilationUnit(loomConfig);
        var projectType = injectInto.Config.ProjectType;
        var sourceFiles = compilationUnit.SourceFiles
            .Where(file =>
                {
                    file.IsIntrinsic = true;
                    if (projectType != ProjectType.Plugin && file.Name == "PluginSecurity.loom")
                        return false;

                    return projectType != ProjectType.Plugin || file.Name != "None.loom";
                }
            )
            .ToList();

        // loom.loom is compiled first, ahead of everything else, and its declarations (luau_name,
        // luau_method, override) are shared with the other intrinsic files via the compilation unit's
        // Globals - the same channel a regular project's .d.loom files use to reach every other file in
        // the unit. This is the only channel intrinsic files have for referencing each other: ambient
        // intrinsic injection (DeclareIntrinsicSymbols) stays off for the whole compile below, guarded by
        // _compilingIntrinsic, to avoid recursing back into this same method.
        var baseFile = sourceFiles.Find(file => file.Name == "loom.loom");
        var baseCompiled = baseFile != null ? compilationUnit.Compile(baseFile) : null;
        if (baseCompiled != null)
            foreach (var symbol in baseCompiled.Tree.Statements.SelectMany(statement => baseCompiled.SemanticModel.GetDeclarationSymbols(statement)))
                compilationUnit.Globals[symbol] = baseCompiled.SemanticModel.GetType(symbol.Declaration);

        var compiledFiles = sourceFiles
            .Where(file => file != baseFile)
            .Select(compilationUnit.Compile)
            .Append(baseCompiled)
            .OfType<CompiledFile>()
            .ToArray();

        var intrinsicSymbols = new HashSet<(Symbol, Type)>();
        foreach (var compiledFile in compiledFiles)
        {
            var symbols = compiledFile.Tree.Statements.SelectMany(statement => compiledFile.SemanticModel.GetDeclarationSymbols(statement));
            foreach (var symbol in symbols)
            {
                symbol.IsIntrinsic = true;
                symbol.IsGlobal = true;
                intrinsicSymbols.Add((symbol, compiledFile.SemanticModel.GetType(symbol.Declaration)));
            }
        }

        _compilingIntrinsic = false;
        return intrinsicSymbols;
    }
}