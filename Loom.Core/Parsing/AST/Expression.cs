using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public abstract class Expression(IEnumerable<Token?> theseTokens, IEnumerable<Node?> children)
    : Node(theseTokens, children)
{
    public new IReadOnlyList<Expression> Children => base.Children.OfType<Expression>().ToList();
}