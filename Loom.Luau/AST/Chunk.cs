using System.Text;

namespace Loom.Luau.AST;

public class Chunk(List<LuauStatement> statements) : LuauStatement
{
    public List<LuauStatement> Statements { get; } = statements;
    public bool IsSimple { get; } = statements is [Continue or Break or Return { Expression: null }];

    public override string Render(RenderState state)
    {
        var renders = Statements.ConvertAll(statement => statement.Render(state));
        return this is LuauTree
            ? RenderLines(state, renders)
            : state.Block(() => RenderLines(state, renders));
    }

    private static string RenderLines(RenderState state, List<string> renders)
    {
        var builder = new StringBuilder();
        foreach (var render in renders)
        {
            var start = 0;
            for (var i = 0; i < render.Length; i++)
            {
                if (render[i] != '\n')
                    continue;

                builder.Append(state.IndentedLine(render[start..i]));
                start = i + 1;
            }

            builder.Append(state.IndentedLine(render[start..]));
        }

        return builder.ToString();
    }
}