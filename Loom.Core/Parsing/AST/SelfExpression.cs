using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public sealed class SelfExpression(Token atToken) : Expression([atToken], [])
{
    public Token AtToken { get; } = atToken;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitSelfExpression(this);
}
