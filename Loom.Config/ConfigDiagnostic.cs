using Tomlyn.Syntax;

namespace Loom.Config;

/// <summary>
///     A problem found while reading <c>loom-config.toml</c>. Config sits below <c>Loom.Core</c>, so manifest problems
///     are reported with these rather than with the compiler's diagnostic bag — and are never thrown.
/// </summary>
public sealed record ConfigDiagnostic(string Message, int? Line = null, int? Column = null)
{
    private const string ReasonSeparator = "Reason: ";
    private const string ErrorMarker = " : error : ";

    /// <summary>
    ///     Rewrites a Tomlyn diagnostic into ours. Tomlyn wraps exceptions thrown by a converter in a sentence naming
    ///     the converter and repeating the position, neither of which means anything to someone editing a manifest, so
    ///     only the reason is kept.
    /// </summary>
    internal static ConfigDiagnostic FromToml(DiagnosticMessage message)
    {
        var text = message.Message;
        var reason = text.LastIndexOf(ReasonSeparator, StringComparison.Ordinal);
        if (reason >= 0)
            text = text[(reason + ReasonSeparator.Length)..];

        if (text.StartsWith('('))
        {
            var marker = text.IndexOf(ErrorMarker, StringComparison.Ordinal);
            if (marker >= 0)
                text = text[(marker + ErrorMarker.Length)..];
        }

        // Tomlyn positions are zero-based; manifests are read by humans counting from one.
        return new ConfigDiagnostic(text.Trim(), message.Span.Start.Line + 1, message.Span.Start.Column + 1);
    }

    public override string ToString() => Line == null ? Message : $"({Line},{Column}): {Message}";
}
