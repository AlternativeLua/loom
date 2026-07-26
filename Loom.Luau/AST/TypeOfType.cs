namespace Loom.Luau.AST;

public class TypeOfType(LuauExpression expression) : LuauType
{
    public LuauExpression Expression { get; } = expression;
    public override string Render(RenderState state) => $"typeof({Expression.Render(state)})";
}