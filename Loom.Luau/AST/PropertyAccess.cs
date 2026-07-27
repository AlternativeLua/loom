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
            if (LuauFactory.Keywords.Contains(name) || !IsValidIdentifier(name))
            {
                result += $"[{RenderState.StringDelimiter}{RenderState.Escape(name)}{RenderState.StringDelimiter}]";
                continue;
            }

            result += (i == Names.Count - 1 ? Operator : '.') + name;
        }

        return result;
    }

    // Not every string that reaches a PropertyAccess (e.g. a string literal key rewritten from an
    // ElementAccess or an `in` check) is a syntactically valid Luau identifier - "foo-bar" or "123"
    // would otherwise render as invalid dotted syntax rather than falling back to bracket indexing.
    private static bool IsValidIdentifier(string name) =>
        name.Length > 0
        && (char.IsAsciiLetter(name[0]) || name[0] == '_')
        && name.Skip(1).All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}