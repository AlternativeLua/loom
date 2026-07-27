namespace Loom.Core.Diagnostics;

/// <summary>
///     How the <see cref="DiagnosticBag" />s of one compilation behave when a diagnostic is reported. A bag
///     carries the options it was created with, so reporting behavior is a property of the compilation that
///     asked for it rather than of the process — two compilations in the same process cannot change how the
///     other reports.
/// </summary>
public sealed record DiagnosticOptions
{
    /// <summary>Used by bags created without options: collect everything, never end the process.</summary>
    public static readonly DiagnosticOptions Default = new();

    /// <summary>
    ///     Print the first error reported to a bag and end the process with exit code 1, instead of
    ///     collecting it and letting the rest of the pipeline run. Meant for the CLI, which has nothing left
    ///     to do once a file fails to compile; off by default so embedding the compiler cannot kill the host.
    /// </summary>
    public bool FailFast { get; init; }
}
