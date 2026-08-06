using Loom.Config;
using Loom.Core.Pipeline;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loom.LanguageServer;

public sealed class DocumentStore
{
    private readonly Dictionary<DocumentUri, string> _documents = [];
    private readonly Dictionary<string, CompilationUnit> _unitsByProjectRoot = [];

    public CompilationResult? Open(DocumentUri uri, string text)
    {
        _documents[uri] = text;
        return Recompile(uri, text);
    }

    public CompilationResult? Change(DocumentUri uri, IEnumerable<TextDocumentContentChangeEvent> changes)
    {
        if (!_documents.TryGetValue(uri, out var text))
            return null;

        text = IncrementalText.ApplyChanges(text, changes);
        _documents[uri] = text;
        return Recompile(uri, text);
    }

    public void Close(DocumentUri uri) => _documents.Remove(uri);

    private CompilationResult? Recompile(DocumentUri uri, string text)
    {
        var path = uri.GetFileSystemPath();
        if (path == null || GetOrCreateUnit(path) is not { } unit)
            return null;

        return unit.Recompile(new Dictionary<string, string> { [path] = text });
    }

    private CompilationUnit? GetOrCreateUnit(string absolutePath)
    {
        var config = LocateProjectConfig(absolutePath);
        if (config == null)
            return null;

        if (_unitsByProjectRoot.TryGetValue(config.ProjectDirectory, out var unit))
            return unit;

        config.NoEmit = true;
        unit = new CompilationUnit(config);
        unit.Compile();
        _unitsByProjectRoot[config.ProjectDirectory] = unit;
        return unit;
    }

    private static LoomConfig? LocateProjectConfig(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        while (directory != null)
        {
            var config = ConfigReader.LocateFromDirectory(directory, out _);
            if (config != null)
                return config;

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
