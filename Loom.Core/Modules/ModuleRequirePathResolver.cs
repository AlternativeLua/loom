using Loom.Config;
using Loom.Core.Pipeline;
using Loom.Core.Text;

namespace Loom.Core.Modules;

/// <summary>
///     Turns a module into the path a <c>require</c> names it by, using the Rojo project to find where the
///     module's compiled output lands in the instance tree.
/// </summary>
/// <remarks>
///     A module from a dependency root is named through the entry project's Rojo project too: that project
///     describes the tree the compiled game actually runs in, whereas a dependency's own project file only
///     describes how that dependency is laid out when it is built alone. Where the module's output lands, on
///     the other hand, is its own root's business — that is the directory its Luau is written to.
/// </remarks>
public sealed class ModuleRequirePathResolver(SourceRootSet roots)
{
    private readonly Dictionary<SourceFile, IReadOnlyList<string>?> _instancePaths = [];
    private readonly RojoResolver? _rojoResolver = RojoResolver.FromProjectDirectory(roots.Entry.Config.ProjectDirectory);

    /// <param name="module">The module file to resolve.</param>
    /// <param name="specifier">The specifier as written, used when Rojo cannot name the module.</param>
    public ModuleRequirePath Resolve(SourceFile module, string specifier)
    {
        if (_rojoResolver == null)
            return ModuleRequirePath.Fallback(ModuleRequirePathStatus.RojoMissing, specifier);

        if (!_instancePaths.TryGetValue(module, out var instancePath))
            _instancePaths[module] = instancePath = _rojoResolver.ResolvePath(FileManager.GetOutputPath(module, roots.ConfigOf(module)));

        return instancePath == null
            ? ModuleRequirePath.Fallback(ModuleRequirePathStatus.NotFoundInRojo, specifier)
            : ModuleRequirePath.Resolved(instancePath);
    }
}