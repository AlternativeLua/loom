namespace Loom.Luau.AST;

public class Parameter(string name, LuauType? declaredType = null)
    : Variable(name, declaredType)
{
    public static Parameter Vararg(LuauType? declaredType = null) => new("...", declaredType);

    public override string Render(RenderState state) => Name + RenderType(state);
}