using Loom.Core.TypeChecking.Types;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loom.LanguageServer;

public sealed class HoverHandler(DocumentStore documents) : HoverHandlerBase
{
    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGetState(request.TextDocument.Uri, out var state))
            return Task.FromResult<Hover?>(null);

        var offset = IncrementalText.ToOffset(state.File.SourceFile.SourceText, request.Position);
        var node = NodeFinder.FindAt(state.File.Tree, offset);
        if (node == null)
            return Task.FromResult<Hover?>(null);

        var type = state.File.SemanticModel.GetType(node);
        if (type is TypeVariable)
            return Task.FromResult<Hover?>(null);

        var hover = new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(new MarkupContent { Kind = MarkupKind.Markdown, Value = $"```loom\n{type}\n```" }),
            Range = Conversion.ToRange(node.LocationSpan)
        };

        return Task.FromResult<Hover?>(hover);
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities) =>
        new() { DocumentSelector = TextDocumentSelector.ForPattern("**/*.loom") };
}
