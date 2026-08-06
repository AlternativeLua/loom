using Loom.Core.Diagnostics;
using Loom.Core.Text;
using Loom.LanguageServer;
using InternalDiagnosticSeverity = Loom.Core.Diagnostics.DiagnosticSeverity;
using LspDiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace Loom.Testing;

[Collection("Assembly")]
public class LanguageServerConversionTest
{
    [Fact]
    public void ToPosition_ConvertsOneBasedLineAndZeroBasedCharacterCorrectly()
    {
        var file = Utility.TestFile("let a = 1;\nlet b = 2;");
        var location = new Location(file, file.SourceText.IndexOf('2'));

        var position = Conversion.ToPosition(location);

        Assert.Equal(1, position.Line);
        Assert.Equal(8, position.Character);
    }

    [Fact]
    public void ToRange_ConvertsStartAndEnd()
    {
        var file = Utility.TestFile("let x = 1;");
        var span = new LocationSpan(new Location(file, 4), new Location(file, 5));

        var range = Conversion.ToRange(span);

        Assert.Equal(0, range.Start.Line);
        Assert.Equal(4, range.Start.Character);
        Assert.Equal(0, range.End.Line);
        Assert.Equal(5, range.End.Character);
    }

    [Theory]
    [InlineData(InternalDiagnosticSeverity.Error, LspDiagnosticSeverity.Error)]
    [InlineData(InternalDiagnosticSeverity.Warn, LspDiagnosticSeverity.Warning)]
    [InlineData(InternalDiagnosticSeverity.Info, LspDiagnosticSeverity.Information)]
    public void ToSeverity_MapsEverySeverity(InternalDiagnosticSeverity loom, LspDiagnosticSeverity lsp) => Assert.Equal(lsp, Conversion.ToSeverity(loom));

    [Fact]
    public void ToDiagnostic_TranslatesRangeSeverityCodeAndMessage()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics("let x: number = \"hi\";");
        var diagnostic = Assert.Single(diagnostics.Set, d => d.Code == InternalCodes.TypeMismatch);

        var lspDiagnostic = Conversion.ToDiagnostic(diagnostic);

        Assert.Equal(Conversion.ToRange(diagnostic.Span), lspDiagnostic.Range);
        Assert.Equal(LspDiagnosticSeverity.Error, lspDiagnostic.Severity);
        Assert.Equal(InternalCodes.TypeMismatch, lspDiagnostic.Code?.String);
        Assert.Equal(diagnostic.Message, lspDiagnostic.Message);
        Assert.Equal("loom", lspDiagnostic.Source);
    }

    [Fact]
    public void ToDiagnostic_AppendsHintToMessage_WhenPresent()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics("let x = 1 + true;");
        var diagnostic = Assert.Single(diagnostics.Set, d => d.Code == InternalCodes.InvalidBinaryOp);
        Assert.NotNull(diagnostic.Hint);

        var lspDiagnostic = Conversion.ToDiagnostic(diagnostic);

        Assert.Equal($"{diagnostic.Message} ({diagnostic.Hint})", lspDiagnostic.Message);
    }
}
