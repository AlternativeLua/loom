using Loom.Core.Parsing.AST;
using Loom.Core.Resolving;
using Loom.Luau;
using Loom.Luau.AST;
using Identifier = Loom.Luau.AST.Identifier;
using PropertyAccess = Loom.Luau.AST.PropertyAccess;
using TypeAlias = Loom.Luau.AST.TypeAlias;
using TypeName = Loom.Luau.AST.TypeName;
using TypeParameter = Loom.Luau.AST.TypeParameter;
using TypeParameters = Loom.Luau.AST.TypeParameters;

namespace Loom.Core.Generation;

public sealed partial class LuauGenerator
{
    private HashSet<string> TakenModuleLocalNames =>
        field ??=
        [
            .._semanticModel.Declarations.Values.SelectMany(symbols => symbols).Select(symbol => symbol.Name),
            .._semanticModel.ImportBindings.Select(binding => binding.LocalName), // aliases live only in the lookup
            LuauFactory.RuntimeImportName
        ];
    
    private List<LuauStatement> GenerateModuleImports()
    {
        var statements = new List<LuauStatement>();
        foreach (var bindings in _semanticModel.ImportBindings.GroupBy(binding => binding.Module))
        {
            var valueBindings = bindings.Where(binding => binding.RequiresModuleAtRuntime).ToList();
            var typeBindings = bindings.Where(binding => binding.Symbol.IsTypeSymbol).ToList();
            if (valueBindings.Count == 0 && typeBindings.Count == 0)
                continue;

            // the output tree mirrors the source tree, so the specifier as written also resolves there
            var specifier = bindings.First().Import.ModulePath!;
            var moduleName = ReserveModuleLocalName(specifier);
            statements.Add(new ConstVariable(moduleName, null, LuauFactory.RequireCall(specifier)));
            statements.AddRange(valueBindings.ConvertAll(binding => GenerateValueImport(binding, moduleName)));
            statements.AddRange(typeBindings.ConvertAll(binding => GenerateTypeImport(binding, moduleName)));
        }

        return statements;
    }

    private static LuauStatement GenerateValueImport(ImportBinding binding, string moduleName) =>
        new ConstVariable(binding.LocalName, null, new PropertyAccess(new Identifier(moduleName), [binding.ExportedName]));
    
    private static LuauStatement GenerateTypeImport(ImportBinding binding, string moduleName)
    {
        var parameterNames = binding.Symbol.Declaration is GenericNamedDeclaration { TypeParameters: { } typeParameters }
            ? typeParameters.ParameterList.ConvertAll(parameter => parameter.Name.Text)
            : [];

        return new TypeAlias(
            binding.LocalName,
            new TypeParameters(parameterNames.ConvertAll(name => new TypeParameter(name))),
            new QualifiedTypeName(
                [moduleName],
                new TypeName(binding.ExportedName, parameterNames.ConvertAll(LuauType (name) => new TypeName(name)))
            )
        );
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
