using System.Text;
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

        /// <summary>
        ///     Runtime origin for bit positions, set while emitting one entry of a collection. Header bits
        ///     normally sit at compile-time positions, but a collection's entries each need their own slice
        ///     of a block whose location is only known once the length has been read.
        /// </summary>
        public LuauExpression? BitBase;

        public LuauExpression BitPosition =>
            BitBase == null ? new NumberLiteral(BitOffset) : Add(BitBase, new NumberLiteral(BitOffset));

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

    /// <summary>
    ///     Reaches a field from the value its enclosing path names. A runtime-kind union's variant carries
    ///     the union's own path rather than one beneath it - the value <em>is</em> the payload - so there
    ///     is nothing left to index.
    /// </summary>
    private static LuauExpression AccessRelative(LuauExpression value, string path, string enclosing) =>
        path == enclosing ? value : Access(value, RelativePath(path, enclosing));

    /// <summary>Strips an enclosing path, leaving the segments to index from the value that path names.</summary>
    private static string RelativePath(string path, string enclosing) =>
        path.StartsWith(enclosing + ".", StringComparison.Ordinal) ? path[(enclosing.Length + 1)..] : path;

    private static LuauExpression Access(LuauExpression source, string path) => new PropertyAccess(source, [..path.Split('.')]);

    /// <summary>
    ///     Last segment of a path, as a usable Luau identifier. Element paths carry brackets - <c>names[]</c>,
    ///     <c>pair[1]</c> - which are neither valid in a name nor distinct from the collection's own local,
    ///     so they become a suffix instead.
    /// </summary>
    private static string LeafName(string path)
    {
        var leaf = path[(path.LastIndexOf('.') + 1)..];
        if (!leaf.Contains('['))
            return leaf;

        // Every bracket group becomes a suffix, not just the first - a nested collection's path carries
        // one per level, and stopping after one leaves the rest in the name.
        var name = new StringBuilder();
        for (var index = 0; index < leaf.Length;)
        {
            if (leaf[index] != '[')
            {
                name.Append(leaf[index++]);
                continue;
            }

            var close = leaf.IndexOf(']', index);
            if (close < 0)
                break;

            var inner = leaf[(index + 1)..close];
            name.Append(inner.Length == 0 ? "_element" : "_" + inner);
            index = close + 1;
        }

        return name.ToString();
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

    // Folded here rather than at each site: a zero-width element makes 'count * 0' fall out of the
    // generic size and bounds expressions, and adding it would cost a multiply on every payload.
    private static LuauExpression Add(LuauExpression left, LuauExpression right) =>
        IsNumber(left, 0) ? right : IsNumber(right, 0) ? left : new BinaryOperator(Operand(left), "+", Operand(right));

    /// <summary>
    ///     Parenthesises an if-expression used as an operand. Luau binds it loosely enough that the else
    ///     branch swallows whatever follows, so a sum of several would nest instead of adding up.
    /// </summary>
    private static LuauExpression Operand(LuauExpression expression) =>
        expression is IfExpression ? new Parenthesized(expression) : expression;
    private static LuauExpression Subtract(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "-", right);
    private static LuauExpression Multiply(LuauExpression left, LuauExpression right) =>
        IsNumber(left, 0) || IsNumber(right, 0)
            ? Zero
            : IsNumber(left, 1) ? right : IsNumber(right, 1) ? left : new BinaryOperator(left, "*", right);

    private static bool IsNumber(LuauExpression expression, double value) => expression is NumberLiteral literal && literal.Value == value;
    private static LuauExpression Divide(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "/", right);
}
