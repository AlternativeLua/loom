using Loom.Core.Text;

namespace Loom.Core.Modules;

/// <param name="CaseInsensitiveMatch">
///     A file the specifier would have named had it been cased the way the file is. Set only alongside
///     <see cref="ModuleResolutionStatus.NotFound" />, since resolution is case-sensitive even where the file
///     system is not.
/// </param>
public sealed record ModuleResolution(ModuleResolutionStatus Status, SourceFile? File, SourceFile? CaseInsensitiveMatch = null)
{
    public static ModuleResolution Resolved(SourceFile file) => new(ModuleResolutionStatus.Resolved, file);
    public static ModuleResolution Failed(ModuleResolutionStatus status) => new(status, null);
    public static ModuleResolution NotFound(SourceFile? caseInsensitiveMatch) => new(ModuleResolutionStatus.NotFound, null, caseInsensitiveMatch);
}