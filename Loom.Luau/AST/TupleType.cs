namespace Loom.Luau.AST;

public class TupleType(List<LuauType> types) : LuauType
{
    public List<LuauType> Types { get; } = types;

    public override string Render(RenderState state) => $"({string.Join(", ", state.RenderList(Types))})";
}
