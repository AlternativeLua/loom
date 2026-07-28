using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public abstract class DestructuringTarget(IEnumerable<Token?> theseTokens, IEnumerable<Node?> children)
    : Node(theseTokens, children);
