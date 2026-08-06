using Loom.Core.Text;
using LoomDiagnostic = Loom.Core.Diagnostics.Diagnostic;
using LoomDiagnosticSeverity = Loom.Core.Diagnostics.DiagnosticSeverity;
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using LspDiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using DiagnosticCode = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticCode;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Loom.LanguageServer;

public static class Conversion
{
    public static Position ToPosition(Location location) => new(location.Line - 1, location.Character);

    public static Range ToRange(LocationSpan span) => new(ToPosition(span.Start), ToPosition(span.End));

    public static LspDiagnosticSeverity ToSeverity(LoomDiagnosticSeverity severity) =>
        severity switch
        {
            LoomDiagnosticSeverity.Error => LspDiagnosticSeverity.Error,
            LoomDiagnosticSeverity.Warn => LspDiagnosticSeverity.Warning,
            _ => LspDiagnosticSeverity.Information
        };

    public static LspDiagnostic ToDiagnostic(LoomDiagnostic diagnostic) =>
        new()
        {
            Range = ToRange(diagnostic.Span),
            Severity = ToSeverity(diagnostic.Severity),
            Code = diagnostic.Code == null ? default : new DiagnosticCode(diagnostic.Code),
            Message = diagnostic.Hint == null ? diagnostic.Message : $"{diagnostic.Message} ({diagnostic.Hint})",
            Source = "loom"
        };
}
