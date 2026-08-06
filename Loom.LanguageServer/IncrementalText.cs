using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Loom.LanguageServer;

public static class IncrementalText
{
    public static string ApplyChange(string text, LspRange? range, string newText)
    {
        if (range == null)
            return newText;

        var start = ToOffset(text, range.Start);
        var end = ToOffset(text, range.End);
        return string.Concat(text.AsSpan(0, start), newText, text.AsSpan(end));
    }

    public static string ApplyChanges(string text, IEnumerable<TextDocumentContentChangeEvent> changes)
    {
        foreach (var change in changes)
            text = ApplyChange(text, change.Range, change.Text);

        return text;
    }

    public static int ToOffset(string text, Position position)
    {
        var offset = 0;
        for (var line = 0; line < position.Line; line++)
        {
            var newlineIndex = text.IndexOf('\n', offset);
            if (newlineIndex < 0)
                return text.Length;

            offset = newlineIndex + 1;
        }

        return Math.Min(offset + position.Character, text.Length);
    }
}
