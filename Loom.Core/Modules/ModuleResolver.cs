using Loom.Core.Pipeline;
using Loom.Core.Text;

namespace Loom.Core.Modules;

/// <summary>
///     Maps a module specifier as written in source (<c>"./math"</c>) onto a <see cref="SourceFile" /> of the
///     compilation unit. Specifiers are extension-less and relative to the importing file's directory;
///     <c>"./math"</c> matches either <c>math.loom</c> or <c>math/init.loom</c>, mirroring how Rojo folds an
///     <c>init</c> file into its folder.
/// </summary>
/// <remarks>
///     Paths are compared case-sensitively even where the file system is not, because Roblox requires are.
///     Declaration files are ambient globals rather than modules, so they are never importable.
///     A relative specifier may not leave the importing file's own root: reaching out of one project and into
///     another is what a package specifier is for, not what <c>"../"</c> is for.
/// </remarks>
public sealed class ModuleResolver
{
    private const string IndexFileName = "init";

    private readonly Dictionary<string, SourceFile> _modulesByPath;

    /// <summary>The same modules keyed without regard to case, to tell a typo from a casing mistake.</summary>
    private readonly Dictionary<string, SourceFile> _modulesByPathIgnoringCase;

    private readonly SourceRootSet _roots;

    public ModuleResolver(IEnumerable<SourceFile> files, SourceRootSet roots)
    {
        _modulesByPath = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        _modulesByPathIgnoringCase = new Dictionary<string, SourceFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(file => !file.IsDeclaration))
        {
            var path = Path.GetFullPath(file.AbsolutePath);
            _modulesByPath.TryAdd(path, file);
            _modulesByPathIgnoringCase.TryAdd(path, file);
        }

        _roots = roots;
    }

    public static bool IsRelativeSpecifier(string specifier) =>
        specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal);

    public ModuleResolution Resolve(SourceFile importingFile, string specifier)
    {
        if (!IsRelativeSpecifier(specifier))
            return ModuleResolution.Failed(ModuleResolutionStatus.UnsupportedSpecifier);

        var importingDirectory = Path.GetDirectoryName(Path.GetFullPath(importingFile.AbsolutePath));
        if (importingDirectory == null)
            return ModuleResolution.Failed(ModuleResolutionStatus.NotFound);

        var basePath = Path.GetFullPath(Path.Combine(importingDirectory, specifier));
        if (!_roots.Of(importingFile).Contains(basePath))
            return ModuleResolution.Failed(ModuleResolutionStatus.OutsideSourceDirectory);

        SourceFile? caseInsensitiveMatch = null;
        foreach (var candidate in GetCandidatePaths(basePath))
        {
            if (!_modulesByPath.TryGetValue(candidate, out var file))
            {
                caseInsensitiveMatch ??= _modulesByPathIgnoringCase.GetValueOrDefault(candidate);
                continue;
            }

            return file == importingFile
                ? ModuleResolution.Failed(ModuleResolutionStatus.SelfImport)
                : ModuleResolution.Resolved(file);
        }

        return ModuleResolution.NotFound(caseInsensitiveMatch);
    }

    /// <summary>The specifier that would have named <paramref name="module" />, relative to the importing file.</summary>
    public static string SpecifierOf(SourceFile importingFile, SourceFile module)
    {
        var importingDirectory = Path.GetDirectoryName(Path.GetFullPath(importingFile.AbsolutePath)) ?? "";
        var relativePath = Path.GetRelativePath(importingDirectory, Path.GetFullPath(module.AbsolutePath));
        var withoutExtension = relativePath[..^FileManager.LoomExtension.Length];
        if (Path.GetFileName(withoutExtension) == IndexFileName)
            withoutExtension = Path.GetDirectoryName(withoutExtension) ?? withoutExtension;

        var specifier = withoutExtension.Replace(Path.DirectorySeparatorChar, '/');
        return specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal) ? specifier : "./" + specifier;
    }

    private static IEnumerable<string> GetCandidatePaths(string basePath)
    {
        yield return basePath + FileManager.LoomExtension;
        yield return Path.Combine(basePath, IndexFileName + FileManager.LoomExtension);
    }
}