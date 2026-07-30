using Loom.Core.Diagnostics;
using Loom.Core.Modules;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving;
using Loom.Core.Resolving.Symbols;
using Loom.Core.Text;
using Loom.Luau;
using Loom.Luau.AST;
using Identifier = Loom.Luau.AST.Identifier;
using PropertyAccess = Loom.Luau.AST.PropertyAccess;
using TypeAlias = Loom.Luau.AST.TypeAlias;
using TypeName = Loom.Luau.AST.TypeName;
using TypeParameter = Loom.Luau.AST.TypeParameter;
using TypeParameters = Loom.Luau.AST.TypeParameters;

namespace Loom.Core.Generation;

/// <summary>
///     Generates the require()/type-import statements a file needs and the export table/type-alias
///     statements it produces, plus resolves which local name a given export's value expression should
///     read through. Only depends on the semantic model and diagnostics - unlike the rest of
///     <see cref="LuauGenerator" /> it never visits AST nodes or touches generation state, so it lives as
///     its own class instead of another <c>LuauGenerator.*.cs</c> partial.
/// </summary>
internal sealed class ModuleImportExportGenerator(SemanticModel semanticModel, DiagnosticBag diagnostics, ModuleRequirePathResolver? moduleRequirePaths)
{
    private readonly Dictionary<SourceFile, string> _moduleLocals = [];

    private HashSet<string> TakenModuleLocalNames =>
        field ??=
        [
            ..semanticModel.Declarations.Values.SelectMany(symbols => symbols).Select(symbol => symbol.Name),
            ..semanticModel.ImportBindings.Select(binding => binding.LocalName), // aliases live only in the lookup
            LuauFactory.RuntimeImportName
        ];

    public List<LuauStatement> GenerateImports()
    {
        var bindingsByModule = semanticModel.ImportBindings
            .GroupBy(binding => binding.Module)
            .ToDictionary(bindings => bindings.Key, bindings => bindings.ToList());

        var statements = new List<LuauStatement>();
        foreach (var (module, specifier, localName) in GetRequiredModules())
        {
            var moduleName = localName ?? ReserveModuleLocalName(specifier);
            _moduleLocals[module] = moduleName;
            statements.Add(new ConstVariable(moduleName, null, LuauFactory.RequireCall(GetRequirePath(module, specifier))));

            var bindings = bindingsByModule.GetValueOrDefault(module, []);
            statements.AddRange(bindings.Where(binding => binding.RequiresModuleAtRuntime).Select(binding => GenerateValueImport(binding, moduleName)));
            statements.AddRange(bindings.Where(binding => binding.Symbol.IsTypeSymbol).Select(binding => GenerateTypeImport(binding, moduleName)));
        }

        return statements;
    }

    private List<(SourceFile Module, string Specifier, string? LocalName)> GetRequiredModules()
    {
        var seen = new HashSet<SourceFile>();

        var modules = semanticModel.NamespaceImports
            .Where(binding => seen.Add(binding.Module))
            .Select((SourceFile, string, string?) (binding) => (binding.Module, binding.ModulePath, binding.LocalName))
            .ToList();

        modules.AddRange(
            semanticModel.ImportBindings.Where(binding => seen.Add(binding.Module))
                .Select((SourceFile, string, string?) (binding) => (binding.Module, binding.Import.ModulePath!, null))
        );

        modules.AddRange(
            semanticModel.Exports.Where(export => export.Module != null && seen.Add(export.Module))
                .Select((SourceFile, string, string?) (export) => (export.Module!, export.ModulePath!, null))
        );

        return modules;
    }

    private string GetRequirePath(SourceFile module, string specifier)
    {
        var requirePath = moduleRequirePaths?.Resolve(module, specifier)
            ?? ModuleRequirePath.Fallback(ModuleRequirePathStatus.RojoMissing, specifier);

        if (requirePath.Status == ModuleRequirePathStatus.NotFoundInRojo)
            diagnostics.Warn(
                semanticModel.Tree,
                InternalCodes.ModuleNotFoundInRojo,
                $"Could not locate module '{specifier}' through the Rojo project; falling back to a relative require.",
                "add a $path mapping to your default.project.json that includes the output directory"
            );

        return requirePath.Path;
    }

    private static ConstVariable GenerateValueImport(ImportBinding binding, string moduleName) =>
        new(binding.LocalName, null, new PropertyAccess(new Identifier(moduleName), [binding.ExportedName]));

    private static TypeAlias GenerateTypeImport(ImportBinding binding, string moduleName) =>
        GenerateTypeAlias(binding.LocalName, binding.ExportedName, binding.Symbol, moduleName, false);

    public LuauExpression GenerateModuleMember(SourceFile module, string name) => new PropertyAccess(new Identifier(_moduleLocals[module]), [name]);

    public LuauExpression GenerateExportedValue(ExportBinding export) =>
        export.Module == null
            ? new Identifier(export.SourceName)
            : new PropertyAccess(new Identifier(_moduleLocals[export.Module]), [export.SourceName]);

    public void MarkListExportedTypes(List<LuauStatement> statements)
    {
        var typeAliasesByName = statements.OfType<TypeAlias>().ToDictionary(a => a.Name, a => a);
        var unaliasedTypeExports = semanticModel.Exports.Where(export => export is { IsReExport: false, Symbol.IsTypeSymbol: true }
            && export.Name == export.SourceName
        );

        foreach (var export in unaliasedTypeExports)
            if (typeAliasesByName.TryGetValue(export.Name, out var typeAlias))
                typeAlias.IsExported = true;
    }

    public IEnumerable<LuauStatement> GenerateExportedTypeAliases() =>
        semanticModel.Exports
            .Where(export => export.Symbol.IsTypeSymbol && (export.IsReExport || export.Name != export.SourceName))
            .Select(export => GenerateTypeAlias(
                    export.Name,
                    export.SourceName,
                    export.Symbol,
                    export.Module == null ? null : _moduleLocals[export.Module],
                    true
                )
            );

    private static TypeAlias GenerateTypeAlias(string name, string sourceName, Symbol symbol, string? moduleName, bool isExported)
    {
        var parameterNames = symbol.Declaration is GenericNamedDeclaration { TypeParameters: { } typeParameters }
            ? typeParameters.ParameterList.ConvertAll(parameter => parameter.Name.Text)
            : [];

        var source = new TypeName(sourceName, parameterNames.ConvertAll(LuauType (parameter) => new TypeName(parameter)));
        return new TypeAlias(
            name,
            new TypeParameters(parameterNames.ConvertAll(parameter => new TypeParameter(parameter))),
            moduleName == null ? source : new QualifiedTypeName([moduleName], source)
        ) { IsExported = isExported };
    }

    private string ReserveModuleLocalName(string specifier)
    {
        var segment = specifier.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(segment => segment != "..");
        var sanitized = new string((segment ?? "module").Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        var name = sanitized.Length == 0 || char.IsDigit(sanitized[0]) ? '_' + sanitized : sanitized;
        if (LuauFactory.Keywords.Contains(name))
            name = '_' + name;

        var unique = name;
        for (var suffix = 1; !TakenModuleLocalNames.Add(unique); suffix++)
            unique = name + '_' + suffix;

        return unique;
    }
}