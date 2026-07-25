using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Text;

namespace Loom.Core.Modules;

/// <summary>
/// The import dependency graph of a compilation unit. Resolves every import specifier to a source file,
/// orders the files so that a module is analyzed before anything importing it, and reports imports that
/// cannot be resolved along with dependency cycles.
/// </summary>
public sealed class ModuleGraph
{
    private readonly Dictionary<NodeId, SourceFile> _resolvedModules;
    private readonly Dictionary<SourceFile, DiagnosticBag> _diagnostics;

    private ModuleGraph(
        List<ParsedFile> order,
        Dictionary<NodeId, SourceFile> resolvedModules,
        Dictionary<SourceFile, DiagnosticBag> diagnostics)
    {
        Order = order;
        _resolvedModules = resolvedModules;
        _diagnostics = diagnostics;
    }

    /// <summary>Every parsed file, dependencies before their importers.</summary>
    public List<ParsedFile> Order { get; }

    /// <summary>The module an import resolves to, or null when resolution failed and was reported.</summary>
    public SourceFile? GetResolvedModule(ImportDeclaration import) => _resolvedModules.GetValueOrDefault(import.Id);

    /// <summary>Module diagnostics belonging to <paramref name="file"/>, reported at its import sites.</summary>
    public DiagnosticBag? GetDiagnostics(SourceFile file) => _diagnostics.GetValueOrDefault(file);

    public static ModuleGraph Build(List<ParsedFile> parsedFiles, LoomConfig config)
    {
        var resolver = new ModuleResolver(parsedFiles.ConvertAll(parsedFile => parsedFile.File), config.Files.SourceDirectory);
        var parsedFilesByFile = new Dictionary<SourceFile, ParsedFile>();
        foreach (var parsedFile in parsedFiles)
            parsedFilesByFile.TryAdd(parsedFile.File, parsedFile);

        var resolvedModules = new Dictionary<NodeId, SourceFile>();
        var diagnostics = new Dictionary<SourceFile, DiagnosticBag>();
        var dependencies = new Dictionary<SourceFile, List<ModuleEdge>>();

        foreach (var parsedFile in parsedFiles)
        {
            var edges = new List<ModuleEdge>();
            foreach (var import in parsedFile.Imports)
            {
                var target = ResolveImport(resolver, parsedFile, import, parsedFilesByFile, diagnostics);
                if (target == null)
                    continue;

                resolvedModules[import.Id] = target.File;
                edges.Add(new ModuleEdge(import, target));
            }

            dependencies[parsedFile.File] = edges;
        }

        var order = Sort(parsedFiles, dependencies, config, diagnostics);
        return new ModuleGraph(order, resolvedModules, diagnostics);
    }

    private static ParsedFile? ResolveImport(
        ModuleResolver resolver,
        ParsedFile parsedFile,
        ImportDeclaration import,
        Dictionary<SourceFile, ParsedFile> parsedFilesByFile,
        Dictionary<SourceFile, DiagnosticBag> diagnostics)
    {
        if (parsedFile.File.IsDeclaration)
        {
            Report(
                diagnostics,
                parsedFile.File,
                import,
                InternalCodes.ImportInDeclarationFile,
                "Declaration files cannot import modules.",
                "declare the symbol ambiently instead"
            );

            return null;
        }

        var specifier = import.ModulePath;
        if (specifier == null)
            return null; // the parser already reported the malformed specifier

        var resolution = resolver.Resolve(parsedFile.File, specifier);
        switch (resolution.Status)
        {
            case ModuleResolutionStatus.Resolved when resolution.File != null:
                return parsedFilesByFile.GetValueOrDefault(resolution.File);

            case ModuleResolutionStatus.UnsupportedSpecifier:
                Report(
                    diagnostics,
                    parsedFile.File,
                    import.ModuleSpecifier,
                    InternalCodes.UnsupportedModuleSpecifier,
                    $"Module '{specifier}' is not a relative path.",
                    "package imports are not supported yet; start the path with './' or '../'"
                );

                return null;

            case ModuleResolutionStatus.SelfImport:
                Report(
                    diagnostics,
                    parsedFile.File,
                    import.ModuleSpecifier,
                    InternalCodes.SelfImport,
                    "A module cannot import itself."
                );

                return null;

            case ModuleResolutionStatus.OutsideSourceDirectory:
                Report(
                    diagnostics,
                    parsedFile.File,
                    import.ModuleSpecifier,
                    InternalCodes.ModuleOutsideSourceDirectory,
                    $"Module '{specifier}' is outside the source directory."
                );

                return null;

            default:
                Report(
                    diagnostics,
                    parsedFile.File,
                    import.ModuleSpecifier,
                    InternalCodes.ModuleNotFound,
                    $"Could not find module '{specifier}'.",
                    specifier.EndsWith(FileManager.LoomExtension, StringComparison.Ordinal)
                        ? $"drop the '{FileManager.LoomExtension}' extension from the path"
                        : null
                );

                return null;
        }
    }

    /// <summary>
    /// Depth-first post-order traversal: a file is appended once every module it imports has been
    /// appended. An edge back into a file still being visited closes a cycle, which is reported at the
    /// import that closed it and then ignored so the remaining files can still be ordered.
    /// </summary>
    private static List<ParsedFile> Sort(
        List<ParsedFile> parsedFiles,
        Dictionary<SourceFile, List<ModuleEdge>> dependencies,
        LoomConfig config,
        Dictionary<SourceFile, DiagnosticBag> diagnostics)
    {
        var order = new List<ParsedFile>(parsedFiles.Count);
        var states = new Dictionary<SourceFile, VisitState>();
        var path = new List<SourceFile>();

        foreach (var parsedFile in parsedFiles)
            visit(parsedFile);

        return order;

        void visit(ParsedFile parsedFile)
        {
            if (states.GetValueOrDefault(parsedFile.File) is VisitState.Ordered)
                return;

            states[parsedFile.File] = VisitState.Visiting;
            path.Add(parsedFile.File);

            foreach (var edge in dependencies.GetValueOrDefault(parsedFile.File, []))
            {
                if (states.GetValueOrDefault(edge.Target.File) is VisitState.Visiting)
                {
                    Report(
                        diagnostics,
                        parsedFile.File,
                        edge.Import,
                        InternalCodes.CircularModuleDependency,
                        $"Circular module dependency: {DescribeCycle(path, edge.Target.File, config)}.",
                        "Luau requires cannot be cyclic; move the shared code into a third module"
                    );

                    continue;
                }

                visit(edge.Target);
            }

            path.RemoveAt(path.Count - 1);
            states[parsedFile.File] = VisitState.Ordered;
            order.Add(parsedFile);
        }
    }

    private static string DescribeCycle(List<SourceFile> path, SourceFile target, LoomConfig config)
    {
        var start = path.IndexOf(target);
        var cycle = path.Skip(start < 0 ? 0 : start).Append(target);
        return string.Join(" → ", cycle.Select(file => file.RelativePath(config.Files.SourceDirectory)));
    }

    private static void Report(
        Dictionary<SourceFile, DiagnosticBag> diagnostics,
        SourceFile file,
        Node node,
        string code,
        string message,
        string? hint = null)
    {
        if (!diagnostics.TryGetValue(file, out var bag))
            diagnostics[file] = bag = new DiagnosticBag();

        bag.Error(node, code, message, hint);
    }

    private enum VisitState
    {
        /// <summary>Must stay the default value: an absent file reads back as this from the state lookup.</summary>
        Unvisited,
        Visiting,
        Ordered
    }

    private sealed record ModuleEdge(ImportDeclaration Import, ParsedFile Target);
}
