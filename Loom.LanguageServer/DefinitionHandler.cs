using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loom.LanguageServer;

public sealed class DefinitionHandler(DocumentStore documents) : DefinitionHandlerBase
{
    public override Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGetState(request.TextDocument.Uri, out var state))
            return Task.FromResult<LocationOrLocationLinks?>(null);

        var offset = IncrementalText.ToOffset(state.File.SourceFile.SourceText, request.Position);
        var node = NodeFinder.FindAt(state.File.Tree, offset);
        var symbol = node == null ? null : state.File.SemanticModel.GetSymbol(node) ?? state.File.SemanticModel.GetDeclarationSymbol(node);
        if (symbol == null || !File.Exists(symbol.Declaration.File.AbsolutePath))
            return Task.FromResult<LocationOrLocationLinks?>(null);

        var location = new Location
        {
            Uri = DocumentUri.FromFileSystemPath(symbol.Declaration.File.AbsolutePath),
            Range = Conversion.ToRange(symbol.Declaration.LocationSpan)
        };

        return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(new LocationOrLocationLink(location)));
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) =>
        new() { DocumentSelector = TextDocumentSelector.ForPattern("**/*.loom") };
}
