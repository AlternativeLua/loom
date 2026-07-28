namespace Loom.Luau.AST;

public class MultiReturn(List<LuauExpression> expressions) : LuauStatement
{
    public List<LuauExpression> Expressions { get; } = expressions;

    public override string Render(RenderState state) => $"return {string.Join(", ", state.RenderList(Expressions))}";
}
