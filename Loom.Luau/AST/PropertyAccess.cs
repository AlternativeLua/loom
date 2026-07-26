namespace Loom.Luau.AST;

public class PropertyAccess(LuauExpression target, List<string> names) : LuauExpression
{
    public char Operator { get; set; } = '.';
    public LuauExpression Target { get; } = target;
    public List<string> Names { get; } = names;

    public override string Render(RenderState state)
    {
        var result = state.ParenthesizeIfNeeded(Target);
        for (var i = 0; i < Names.Count; i++)
        {
            var name = Names[i];
            if (LuauFactory.Keywords.Contains(name))
            {
                result += $"[{RenderState.StringDelimiter}{RenderState.Escape(name)}{RenderState.StringDelimiter}]";
                continue;
            }

            result += (i == Names.Count - 1 ? Operator : '.') + name;
        }

        return result;
    }
}