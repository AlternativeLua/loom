using Loom.Core.Parsing.AST;

namespace Loom.LanguageServer;

public static class NodeFinder
{
    public static Node? FindAt(Node root, int position)
    {
        if (position < root.Span.Position || position > root.Span.End)
            return null;

        foreach (var child in root.Children)
        {
            var found = FindAt(child, position);
            if (found != null)
                return found;
        }

        return root;
    }
}
