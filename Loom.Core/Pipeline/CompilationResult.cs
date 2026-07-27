using Loom.Core.Diagnostics;

namespace Loom.Core.Pipeline;

public sealed record CompilationResult(List<CompiledFile> Files, DiagnosticBag Diagnostics)
    : DiagnosedResult(Diagnostics)
{
    /// <summary>
    ///     Files the compiler gave up on. Their diagnostics are part of <see cref="Diagnostics" /> as well;
    ///     this names which files are missing from <see cref="Files" /> and why.
    /// </summary>
    public List<FailedFile> Failures { get; init; } = [];
}