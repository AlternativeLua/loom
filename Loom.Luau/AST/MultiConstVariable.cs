namespace Loom.Luau.AST;

public class MultiConstVariable(List<string> names, LuauExpression initializer) : LuauStatement
{
    public List<string> Names { get; } = names;
    public LuauExpression Initializer { get; } = initializer;

    public override string Render(RenderState state) => $"const {string.Join(", ", Names)} = {Initializer.Render(state)}";
}
