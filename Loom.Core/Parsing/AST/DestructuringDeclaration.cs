using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class DestructuringDeclaration(Token keyword, DestructuringTarget target, ColonTypeClause? colonTypeClause, EqualsValueClause? equalsValueClause)
    : Statement([keyword], [target, colonTypeClause, equalsValueClause])
{
    public Token Keyword { get; } = keyword;
    public DestructuringTarget Target { get; } = target;
    public ColonTypeClause? ColonTypeClause { get; } = colonTypeClause;
    public EqualsValueClause? EqualsValueClause { get; } = equalsValueClause;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitDestructuringDeclaration(this);
}
