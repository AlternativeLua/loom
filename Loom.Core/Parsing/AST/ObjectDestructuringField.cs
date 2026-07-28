using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class ObjectDestructuringField(Token name, Token? colon, Token? alias)
    : Node([name, colon, alias], [])
{
    public Token Name { get; } = name;
    public Token? Colon { get; } = colon;
    public Token? Alias { get; } = alias;
    public Token BindingName => Alias ?? Name;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitObjectDestructuringField(this);
}
