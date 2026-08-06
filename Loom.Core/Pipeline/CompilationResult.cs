using Loom.Core.Diagnostics;
using Loom.Core.Text;

namespace Loom.Core.Pipeline;

public sealed record CompilationResult(List<CompiledFile> Files, DiagnosticBag Diagnostics)
    : DiagnosedResult(Diagnostics)
{
    /// <summary>
    ///     Files the compiler gave up on. Their diagnostics are part of <see cref="Diagnostics" /> as well;
    ///     this names which files are missing from <see cref="Files" /> and why.
    /// </summary>
    public List<FailedFile> Failures { get; init; } = [];

    /// <summary>Files actually re-parsed and re-analyzed this call, as opposed to reused from a prior compile's cache.</summary>
    public IReadOnlyList<SourceFile> Reanalyzed { get; init; } = [];

    /// <summary>Wall-clock time the call took, start to finish.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    ///     Sum of the last-measured analysis time of every file this call skipped reusing from cache. Zero for
    ///     a full <see cref="CompilationUnit.Compile()" />, since nothing is skipped there.
    /// </summary>
    public TimeSpan EstimatedTimeSaved { get; init; }
}