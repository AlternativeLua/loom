using Loom.Core.Diagnostics;
using Loom.Core.Text;

namespace Loom.Core.Pipeline;

/// <summary>
///     A file whose compilation was abandoned because a stage threw — a compiler bug rather than anything
///     wrong with the source. It has no <see cref="CompiledFile" /> to carry its diagnostics, so it is
///     reported through this instead of being dropped.
/// </summary>
public sealed record FailedFile(SourceFile File, DiagnosticBag Diagnostics)
    : DiagnosedResult(Diagnostics);
