using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class Is(Expression expression, Token keyword, TypePattern pattern)
    : Expression([..expression.Tokens, keyword, ..pattern.Tokens], [expression, pattern])
{
    public Expression Expression { get; } = expression;
    public Token Keyword { get; } = keyword;
    public TypePattern Pattern { get; } = pattern;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitIs(this);
}
