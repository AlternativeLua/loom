using System.Diagnostics.CodeAnalysis;
using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving.Symbols;
using Loom.Core.Text;
using Loom.Core.TypeChecking.Types;
using Loom.Luau;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.Resolving;

public sealed partial class Resolver
{
    public override bool VisitExportDeclaration(ExportDeclaration export)
    {
        if (!AtModuleScope())
        {
            _diagnostics.Error(
                export,
                InternalCodes.ExportOutsideModuleScope,
                "Declarations can only be exported at the top level of a module.",
                "move the 'export' declaration out of the enclosing block"
            );

            return false;
        }

        if (export.Declaration is VariableDeclaration { Keyword.Kind: SyntaxKind.MutKeyword })
        {
            _diagnostics.Error(
                export,
                InternalCodes.CannotExportMutable,
                "Mutable variables cannot be exported.",
                "use 'let' instead of 'mut'"
            );

            return false;
        }

        if (!Visit(export.Declaration))
            return false;

        foreach (var symbol in _semanticModel.GetDeclarationSymbols(export.Declaration))
            AddExport(export.Declaration, ExportBinding.OfDeclaration(symbol));

        return true;
    }

    public override bool VisitExportList(ExportList export)
    {
        if (!AtModuleScope())
        {
            _diagnostics.Error(
                export,
                InternalCodes.ExportOutsideModuleScope,
                "Declarations can only be exported at the top level of a module.",
                "move the 'export' declaration out of the enclosing block"
            );

            return false;
        }

        if (!export.IsReExport)
        {
            foreach (var specifier in export.Specifiers)
                ResolveLocalExport(export, specifier);

            return true;
        }

        if (!TryGetModule(export, out var module, out var moduleModel))
            return true; // a re-export binds nothing locally, so there is nothing to stand in for

        foreach (var specifier in export.Specifiers)
            ResolveReExport(export, specifier, module, moduleModel);

        return true;
    }

    public override bool VisitNamespaceImport(NamespaceImport import)
    {
        if (!_resolvedImports.Add(import))
            return true; // already bound when the file's imports were resolved ahead of its statements

        if (!AtModuleScope())
        {
            _diagnostics.Error(
                import,
                InternalCodes.ImportOutsideModuleScope,
                "Modules can only be imported at the top level of a module.",
                "move the 'import' declaration out of the enclosing block"
            );

            return false;
        }

        var name = import.Name.Text;
        if (!TryGetModule(import, out var module, out var moduleModel))
            return DeclareUnresolvedSymbols(import, name, false);

        if (HasDuplicateSymbol(import, name, true, $"Variable '{name}' is already declared in this scope."))
            return true;

        // the symbol stands for the required table, so unlike a named import it is declared on this node
        var symbol = new Symbol(import, SymbolKind.Variable, name);
        DeclareSymbol(symbol);
        _semanticModel.AddNamespaceImport(new NamespaceImportBinding(import, symbol, module));
        _semanticModel.TypeSolver.SetType(import, GetNamespaceType(moduleModel));

        return true;
    }

    /// <summary>
    ///     The module a specifier names, along with its analyzed form. False when the module graph could not
    ///     resolve the specifier — which it has already reported — or when the module has not been analyzed,
    ///     which only a dependency cycle can cause and the graph has reported that too.
    /// </summary>
    private bool TryGetModule(Node moduleReference, [NotNullWhen(true)] out SourceFile? module, [NotNullWhen(true)] out SemanticModel? moduleModel)
    {
        module = compilationUnit.ModuleGraph?.GetResolvedModule(moduleReference);
        moduleModel = module == null ? null : compilationUnit.AnalyzedModules.GetValueOrDefault(module);

        return module != null && moduleModel != null;
    }

    /// <summary>
    ///     Binds the names an import was meant to bring in when its module could not be resolved, so the
    ///     module error stands on its own. Left unbound they would be reported again at every use, and an
    ///     unbound name in type position reaches the generator as a hole it cannot fill.
    /// </summary>
    private bool DeclareUnresolvedImport(ImportDeclaration import)
    {
        foreach (var specifier in import.Specifiers)
            DeclareUnresolvedSymbols(specifier, specifier.LocalName.Text, import.IsTypeOnly);

        return true;
    }

    /// <remarks>
    ///     Nothing is known about what the module exported, so the name stands for a value and a type alike
    ///     unless the import was type-only, and is typed <c>unknown</c> rather than inferred from a use.
    ///     A name already declared here keeps its own declaration: the module error is the one worth
    ///     reporting, not a duplicate the user cannot act on.
    /// </remarks>
    private bool DeclareUnresolvedSymbols(Node declaration, string name, bool isTypeOnly)
    {
        if (!isTypeOnly)
            declareUnresolved(SymbolKind.Variable);

        declareUnresolved(SymbolKind.Type);
        return true;

        void declareUnresolved(SymbolKind kind)
        {
            if (LookupSymbolCurrentScope(name, kind) != null)
                return;

            var symbol = new Symbol(declaration, kind, name);
            DeclareSymbol(symbol);

            // it names something declared elsewhere, so flow analysis must not read the import as the
            // declaration of an uninitialized variable
            _semanticModel.AddUnresolvedImport(symbol);
            _semanticModel.TypeSolver.SetType(declaration, PrimitiveType.Unknown);
        }
    }

    /// <summary>
    ///     The type of a namespace import: an object whose properties are the module's runtime exports, so
    ///     member access on it type-checks against what the module actually returns.
    /// </summary>
    private static Type GetNamespaceType(SemanticModel moduleModel) =>
        new ObjectType(
            null,
            moduleModel.Exports
                .FindAll(export => export.EmitsRuntimeBinding)
                .ConvertAll(export => new ObjectProperty(false, export.Name, moduleModel.GetType(export.Symbol.Declaration)))
        );

    /// <summary>Exports a name the module already declares, without introducing a new binding.</summary>
    private void ResolveLocalExport(ExportList export, ExportSpecifier specifier)
    {
        var name = specifier.Name.Text;
        var typeSymbol = LookupTypeSymbol(name);
        var valueSymbol = export.IsTypeOnly ? null : LookupValueSymbol(name);
        if (typeSymbol == null && valueSymbol == null)
        {
            _diagnostics.Error(
                specifier,
                export.IsTypeOnly ? InternalCodes.TypeOnlyExportOfValue : InternalCodes.CannotFindSymbol,
                export.IsTypeOnly && LookupValueSymbol(name) != null
                    ? $"'{name}' is a value, not a type."
                    : $"Cannot find symbol '{name}'.",
                export.IsTypeOnly && LookupValueSymbol(name) != null ? "remove 'type' from the export" : null
            );

            return;
        }

        foreach (var symbol in new[] { valueSymbol, typeSymbol }.OfType<Symbol>())
        {
            AddReference(specifier, symbol);
            AddExport(specifier, new ExportBinding(specifier.ExportedName.Text, name, symbol, export));
        }
    }

    /// <summary>Forwards another module's export without binding it in this module's scope.</summary>
    private void ResolveReExport(ExportList export, ExportSpecifier specifier, SourceFile module, SemanticModel moduleModel)
    {
        var name = specifier.Name.Text;
        var exports = moduleModel.FindExports(name);
        if (exports.Count == 0)
        {
            _diagnostics.Error(specifier, InternalCodes.NoExportedMember, $"Module '{module.Name}' does not export '{name}'.");
            return;
        }

        if (export.IsTypeOnly)
        {
            var typeExports = exports.FindAll(binding => binding.Symbol.IsTypeSymbol);
            if (typeExports.Count == 0)
            {
                _diagnostics.Error(
                    specifier,
                    InternalCodes.TypeOnlyExportOfValue,
                    $"'{name}' is a value, not a type.",
                    "remove 'type' from the export"
                );

                return;
            }

            exports = typeExports;
        }

        foreach (var binding in exports)
            AddExport(specifier, new ExportBinding(specifier.ExportedName.Text, name, binding.Symbol, export, module));
    }

    private void AddExport(Node node, ExportBinding binding)
    {
        var existing = _semanticModel.FindExports(binding.Name);
        if (existing.Exists(other => other.Symbol.IsTypeSymbol == binding.Symbol.IsTypeSymbol))
        {
            _diagnostics.Error(node, InternalCodes.DuplicateExport, $"'{binding.Name}' is already exported.");
            return;
        }

        _semanticModel.AddExport(binding);
    }

    public override bool VisitImportDeclaration(ImportDeclaration import)
    {
        if (!_resolvedImports.Add(import))
            return true; // already bound when the file's imports were resolved ahead of its statements

        if (!AtModuleScope())
        {
            _diagnostics.Error(
                import,
                InternalCodes.ImportOutsideModuleScope,
                "Modules can only be imported at the top level of a module.",
                "move the 'import' declaration out of the enclosing block"
            );

            return false;
        }

        if (!TryGetModule(import, out var module, out var moduleModel))
            return DeclareUnresolvedImport(import);

        var localNames = new HashSet<string>();
        return import.Specifiers.All(specifier => ResolveImportSpecifier(import, specifier, module, moduleModel, localNames));
    }

    private bool ResolveImportSpecifier(
        ImportDeclaration import,
        ImportSpecifier specifier,
        SourceFile module,
        SemanticModel moduleModel,
        HashSet<string> localNames)
    {
        var name = specifier.Name.Text;
        var localName = specifier.LocalName.Text;
        var exports = moduleModel.FindExports(name);
        if (exports.Count == 0)
        {
            var exported = moduleModel.Exports.Select(symbol => symbol.Name).Distinct().ToList();
            _diagnostics.Error(
                specifier,
                InternalCodes.NoExportedMember,
                $"Module '{module.Name}' does not export '{name}'.",
                exported.Count > 0 ? $"it exports {string.Join(", ", exported.Select(n => $"'{n}'"))}" : "it exports nothing"
            );

            return false;
        }

        if (!localNames.Add(localName))
        {
            _diagnostics.Error(specifier, InternalCodes.DuplicateImport, $"'{localName}' is imported more than once.");
            return false;
        }

        if (!import.IsTypeOnly)
            return exports.All(export => DeclareImportedSymbol(import, specifier, export.Symbol, module, moduleModel));

        var typeExports = exports.FindAll(export => export.Symbol.IsTypeSymbol);
        if (typeExports.Count != 0)
            return typeExports.All(export => DeclareImportedSymbol(import, specifier, export.Symbol, module, moduleModel));

        _diagnostics.Error(
            specifier,
            InternalCodes.TypeOnlyImportOfValue,
            $"'{name}' is a value, not a type.",
            "remove 'type' from the import"
        );

        return false;
    }

    /// <summary>
    ///     Binds the exporting module's own symbol instance into this scope under the local name. The instance
    ///     is reused rather than copied — the same thing <see cref="DeclareGlobalSymbols" /> does for globals —
    ///     so that an imported interface still resolves to an <see cref="InterfaceSymbol" />.
    /// </summary>
    private bool DeclareImportedSymbol(
        ImportDeclaration import,
        ImportSpecifier specifier,
        Symbol export,
        SourceFile module,
        SemanticModel moduleModel)
    {
        var localName = specifier.LocalName.Text;
        var duplicateKind = export.IsTypeSymbol ? "Type" : "Variable";
        if (HasDuplicateSymbol(specifier, localName, !export.IsTypeSymbol, $"{duplicateKind} '{localName}' is already declared in this scope."))
            return false;

        if (LuauFactory.Keywords.Contains(localName))
        {
            _diagnostics.Error(
                specifier,
                InternalCodes.ReservedLuauKeyword,
                $"'{localName}' is a reserved Luau keyword and cannot be used as a declaration name."
            );

            return false;
        }

        AddToLookup(localName, export);
        AddDeclaration(export);
        _semanticModel.AddImportBinding(new ImportBinding(import, specifier, export, module));
        _semanticModel.TypeSolver.SetType(export.Declaration, moduleModel.GetType(export.Declaration));
        return true;
    }

    /// <remarks>
    ///     One specifier can produce a binding per namespace, so an interface referenced only as a type still
    ///     counts as used. Runs once the whole tree is resolved, since a name may be used above its import.
    /// </remarks>
    private void ReportUnusedImports()
    {
        foreach (var bindings in _semanticModel.ImportBindings.GroupBy(binding => binding.Specifier))
        {
            if (bindings.Any(binding => binding.IsUsed))
                continue;

            var binding = bindings.First();
            _diagnostics.Warn(
                binding.Specifier,
                InternalCodes.UnusedImport,
                $"'{binding.LocalName}' is imported but never used.",
                "remove it from the import clause"
            );
        }

        foreach (var binding in _semanticModel.NamespaceImports.Where(binding => !binding.IsUsed))
            _diagnostics.Warn(
                binding.Import,
                InternalCodes.UnusedImport,
                $"'{binding.LocalName}' is imported but never used.",
                "remove the import"
            );
    }
}
