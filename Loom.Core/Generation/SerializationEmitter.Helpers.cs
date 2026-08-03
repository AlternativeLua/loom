using Loom.Core.TypeChecking.Serialization;
using Loom.Core.TypeChecking.Types;
using Loom.Luau;
using Loom.Luau.AST;

namespace Loom.Core.Generation;

/// <summary>Shared pieces: the offset cursor, local-name reservation, and expression builders.</summary>
internal sealed partial class SerializationEmitter
{
    private static readonly List<string> CFramePositionComponents = ["X", "Y", "Z"];
    private static readonly List<string> QuaternionLocals = ["qx", "qy", "qz", "qw"];
    private static readonly NumberLiteral Zero = new(0);
    private static readonly NumberLiteral One = new(1);

    /// <summary>Running header-bit and body-byte positions, both compile-time constants.</summary>
    /// <summary>
    ///     Tracks where the next write lands. Offsets stay compile-time constants until a value-dependent
    ///     field is reached; from there a runtime local carries the position, seeded with the constant
    ///     the cursor had already accumulated. A fully fixed schema never leaves the constant path.
    /// </summary>
    private sealed class Cursor(int startingByteOffset)
    {
        public int BitOffset;
        public int ByteOffset = startingByteOffset;
        public bool IsDynamic;

        public LuauExpression Position => IsDynamic ? new Identifier(OffsetLocal) : new NumberLiteral(ByteOffset);

        public void Advance(List<LuauStatement> body, int bytes)
        {
            if (!IsDynamic)
            {
                ByteOffset += bytes;
                return;
            }

            body.Add(new ExpressionStatement(new BinaryOperator(new Identifier(OffsetLocal), "+=", new NumberLiteral(bytes))));
        }

        public void AdvanceBy(List<LuauStatement> body, LuauExpression bytes)
        {
            GoDynamic(body);
            body.Add(new ExpressionStatement(new BinaryOperator(new Identifier(OffsetLocal), "+=", bytes)));
        }

        public void GoDynamic(List<LuauStatement> body)
        {
            if (IsDynamic)
                return;

            body.Add(new LocalVariable(OffsetLocal, null, new NumberLiteral(ByteOffset)));
            IsDynamic = true;
        }
    }

    /// <summary>Binds an expression to a local when it is about to be read more than once.</summary>
    private LuauExpression BindIfReused(LuauExpression value, int uses, string preferred, List<LuauStatement> body)
    {
        if (uses < 2 || value is Identifier)
            return value;

        var local = ReserveLocal(preferred + "_value");
        body.Add(new ConstVariable(local, null, value));

        return new Identifier(local);
    }

    private static LuauExpression Access(LuauExpression source, string path) => new PropertyAccess(source, [..path.Split('.')]);

    /// <summary>
    ///     Last segment of a path, as a usable Luau identifier. Element paths carry brackets - <c>names[]</c>,
    ///     <c>pair[1]</c> - which are neither valid in a name nor distinct from the collection's own local,
    ///     so they become a suffix instead.
    /// </summary>
    private static string LeafName(string path)
    {
        var leaf = path[(path.LastIndexOf('.') + 1)..];
        var bracket = leaf.IndexOf('[');
        if (bracket < 0)
            return leaf;

        var index = leaf[(bracket + 1)..].TrimEnd(']');
        return leaf[..bracket] + (index.Length == 0 ? "_element" : "_" + index);
    }

    private static LuauExpression ToLiteral(object? value) =>
        value switch
        {
            string s => new StringLiteral(s),
            bool b => new BooleanLiteral(b),
            double d => new NumberLiteral(d),
            long l => new NumberLiteral(l),
            int i => new NumberLiteral(i),
            _ => new NilLiteral()
        };

    private static LuauExpression Add(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "+", right);
    private static LuauExpression Subtract(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "-", right);
    private static LuauExpression Multiply(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "*", right);
    private static LuauExpression Divide(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "/", right);
}
