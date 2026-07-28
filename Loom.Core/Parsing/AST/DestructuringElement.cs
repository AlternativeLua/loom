using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class DestructuringElement(Token name)
    : Node([name], [])
{
    public Token Name { get; } = name;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitDestructuringElement(this);
}
