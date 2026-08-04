using Loom.Core.TypeChecking.Serialization;
using Loom.Luau;
using Loom.Luau.AST;

namespace Loom.Core.Generation;

/// <summary>Writing: size computation, the sentinel and union prologue, and per-field writes.</summary>
internal sealed partial class SerializationEmitter
{
    public Function EmitSerializer()
    {
        _locals.Clear();
        var body = new List<LuauStatement>();
        if (schema.HasBlobs)
            body.Add(new ConstVariable(BlobsLocal, null, Table.Empty));

        EmitSentinelPrologue(body);

        if (!schema.IsEmpty)
            body.Add(new ConstVariable(BufferLocal, null, BufferCall("create", [EmitSizeComputation(body)])));

        // Walked even for a zero-byte schema: blob fields still have to be appended, and a schema is
        // only empty when nothing writes bytes or bits, so the absent buffer local is never referenced.
        var cursor = new Cursor(schema.HeaderBytes);
        foreach (var serializationField in schema.Fields)
            EmitWrite(serializationField, new Identifier(ValueParameter), cursor, body);

        body.Add(new Return(BuildSerializedTable()));
        return new Function(
            SerializeName(schema.Interface.Name),
            null,
            [new Parameter(ValueParameter, new TypeName(schema.Interface.Name))],
            LuauFactory.QualifyRuntimeType(new TypeName("Serialized")),
            new Chunk(body)
        );
    }

    /// <summary>
    ///     Binds each sentinelled field's value and resolves which sentinel it matched, ahead of the
    ///     allocation that depends on the answer. A match writes no components at all, so the width is
    ///     not known until the comparison chain has run.
    /// </summary>
    private void EmitSentinelPrologue(List<LuauStatement> body)
    {
        foreach (var serializationField in schema.Fields)
        {
            if (serializationField is UnionField unionField)
            {
                EmitUnionTag(unionField, Access(new Identifier(ValueParameter), unionField.Path), body);
                _prologueTags.Add(unionField.Path);
                continue;
            }

            if (SentinelNamesOf(serializationField) is not { Count: > 0 } sentinels)
                continue;

            var valueLocal = SentinelValueLocal(serializationField.Path);
            body.Add(new ConstVariable(valueLocal, null, Access(new Identifier(ValueParameter), serializationField.Path)));
            body.Add(new LocalVariable(SentinelIndexLocal(serializationField.Path), null, Zero));

            // Index zero stays reserved for "no match, components follow".
            var branches = new List<ElseIfBranch>();
            for (var index = 0; index < sentinels.Count; index++)
            {
                var condition = new BinaryOperator(new Identifier(valueLocal), "==", new Identifier(sentinels[index]));
                var assign = new Chunk(
                    [
                        new ExpressionStatement(
                            new BinaryOperator(new Identifier(SentinelIndexLocal(serializationField.Path)), "=", new NumberLiteral(index + 1))
                        )
                    ]
                );

                if (index == 0)
                    body.Add(new IfStatement(condition, assign, branches, null));
                else
                    branches.Add(new ElseIfBranch(condition, assign));
            }
        }
    }

    /// <summary>Luau-side sentinel constants for a field, or null when it is not sentinelled.</summary>
    private static IReadOnlyList<string>? SentinelNamesOf(SerializationField serializationField) =>
        serializationField switch
        {
            DatatypeField { UseSentinels: true } datatypeField => datatypeField.Datatype.Sentinels,
            CFrameField { UseSentinels: true } => CFrameSentinels,
            _ => null
        };

    private static string SentinelValueLocal(string path) => LeafName(path) + "_value";
    private static string SentinelIndexLocal(string path) => LeafName(path) + "_sentinel";
    private static string UnionTagLocal(string path) => LeafName(path) + "_tag";

    /// <summary>
    ///     Resolves which variant a union value is, ahead of the allocation that depends on it. Index zero
    ///     is the fallback rather than a reserved escape - the type guarantees some variant matches, so
    ///     the first needs no test of its own.
    /// </summary>
    private static void EmitUnionTag(UnionField unionField, LuauExpression value, List<LuauStatement> body)
    {
        var valueLocal = SentinelValueLocal(unionField.Path);
        var tagLocal = UnionTagLocal(unionField.Path);
        body.Add(new ConstVariable(valueLocal, null, value));
        body.Add(new LocalVariable(tagLocal, null, Zero));

        var branches = new List<ElseIfBranch>();
        for (var index = 1; index < unionField.Variants.Count; index++)
        {
            var condition = VariantCondition(unionField, new Identifier(valueLocal), index);
            var assign = new Chunk([new ExpressionStatement(new BinaryOperator(new Identifier(tagLocal), "=", new NumberLiteral(index)))]);
            if (index == 1)
                body.Add(new IfStatement(condition, assign, branches, null));
            else
                branches.Add(new ElseIfBranch(condition, assign));
        }
    }

    /// <summary>Test that identifies a variant, by literal value, runtime kind, or shared discriminant.</summary>
    private static BinaryOperator VariantCondition(UnionField unionField, LuauExpression value, int index)
    {
        var discriminant = unionField.Variants[index].Discriminant;
        return unionField.Discrimination switch
        {
            UnionDiscrimination.LiteralValue => new BinaryOperator(value, "==", ToLiteral(discriminant)),
            UnionDiscrimination.RuntimeKind => new BinaryOperator(
                new Call(new Identifier("typeof"), [value]),
                "==",
                new StringLiteral((string)discriminant!)
            ),
            _ => new BinaryOperator(new PropertyAccess(value, [unionField.DiscriminantName!]), "==", ToLiteral(discriminant))
        };
    }

    private static int VariantBytes(SerializationVariant variant) => variant.Fields.Sum(f => f.BodyBytes ?? 0);

    /// <summary>
    ///     A variant's width, or null when it writes nothing. Variable-width fields inside a variant still
    ///     need measuring - a string variant sized only by its fixed part would under-allocate and then
    ///     overrun the buffer it was given.
    /// </summary>
    private static LuauExpression? VariantSizeExpression(UnionField unionField, SerializationVariant variant, LuauExpression value)
    {
        var constant = 0;
        var measured = new List<LuauExpression>();
        foreach (var (variantField, variantValue) in ChildrenOf(unionField, variant, value))
        {
            if (variantField.BodyBytes is { } fixedBytes)
            {
                constant += fixedBytes;
                continue;
            }

            if (InlineContribution(variantField, variantValue) is { } contribution)
                measured.Add(contribution);
        }

        if (measured.Count == 0)
            return constant > 0 ? new NumberLiteral(constant) : null;

        var total = measured.Aggregate(Add);
        return constant > 0 ? Add(new NumberLiteral(constant), total) : total;
    }

    /// <summary>
    ///     Computes the total width, appending statements to <paramref name="body" /> only when some field
    ///     cannot state its contribution as an expression. A fixed schema folds to a literal, widths that
    ///     are inline-expressible stay in one arithmetic expression, and anything needing control flow -
    ///     an array of variable-width elements, a union whose width follows its tag - falls back to
    ///     accumulating into a local.
    /// </summary>
    private LuauExpression EmitSizeComputation(List<LuauStatement> body)
    {
        if (schema.FixedByteCount is { } fixedByteCount)
            return new NumberLiteral(fixedByteCount);

        var constant = schema.HeaderBytes + schema.Fields.Sum(f => f.BodyBytes ?? 0);
        LuauExpression? inline = constant > 0 ? new NumberLiteral(constant) : null;
        var traversal = new List<LuauStatement>();
        foreach (var serializationField in schema.Fields)
        {
            var value = Access(new Identifier(ValueParameter), serializationField.Path);
            if (InlineContribution(serializationField, value) is { } contribution)
            {
                inline = inline == null ? contribution : Add(inline, contribution);
                continue;
            }

            // Anything with a known width is already folded into the constant above.
            if (serializationField.BodyBytes != null)
                continue;

            EmitMeasure(serializationField, value, traversal);
        }

        if (traversal.Count == 0)
            return inline ?? Zero;

        body.Add(new LocalVariable(SizeLocal, null, inline ?? Zero));
        body.AddRange(traversal);

        return new Identifier(SizeLocal);
    }

    /// <summary>A field's contribution as an expression, or null when it needs the traversal.</summary>
    private static LuauExpression? InlineContribution(SerializationField serializationField, LuauExpression value)
    {
        if (SentinelNamesOf(serializationField) is { Count: > 0 })
            return new IfExpression(
                new BinaryOperator(new Identifier(SentinelIndexLocal(serializationField.Path)), "==", Zero),
                new NumberLiteral(SentinelComponentBytes(serializationField)),
                [],
                Zero
            );

        return serializationField switch
        {
            StringField stringField => Add(new NumberLiteral(stringField.LengthType.ByteCount()), Length(value)),
            ArrayField { Element.BodyBytes: { } elementBytes } arrayField =>
                Add(
                    Add(new NumberLiteral(arrayField.LengthType.ByteCount()), Multiply(Length(value), new NumberLiteral(elementBytes))),
                    ElementBitBlockSize(arrayField.Element.HeaderBits, Length(value))
                ),
            OptionalField { Inner.BodyBytes: 0 } => Zero,
            OptionalField { Inner.BodyBytes: { } innerBytes } =>
                new IfExpression(IsPresent(value), new NumberLiteral(innerBytes), [], Zero),
            _ => null
        };
    }

    /// <summary>Accumulates a field's width into the size local, for shapes that need control flow.</summary>
    private void EmitMeasure(SerializationField serializationField, LuauExpression value, List<LuauStatement> statements)
    {
        // A flattened nested struct contributes each of its parts.
        if (serializationField is TupleField tupleField)
        {
            foreach (var (part, partValue) in ChildrenOf(tupleField, value))
                MeasureField(part, partValue, statements);

            return;
        }

        // A payload that is only sometimes written is only sometimes counted.
        if (serializationField is OptionalField optionalField)
        {
            var innerStatements = new List<LuauStatement>();
            foreach (var (inner, innerValue) in ChildrenOf(optionalField, value))
                MeasureField(inner, innerValue, innerStatements);

            if (innerStatements.Count > 0)
                statements.Add(new IfStatement(IsPresent(value), new Chunk(innerStatements), [], null));

            return;
        }

        if (serializationField is UnionField unionField)
        {
            if (!_prologueTags.Contains(unionField.Path))
                EmitUnionTag(unionField, value, statements);

            var tag = new Identifier(UnionTagLocal(unionField.Path));
            var branches = new List<ElseIfBranch>();
            IfStatement? head = null;
            for (var index = 0; index < unionField.Variants.Count; index++)
            {
                if (VariantSizeExpression(unionField, unionField.Variants[index], value) is not { } variantSize)
                    continue;

                var condition = new BinaryOperator(tag, "==", new NumberLiteral(index));
                var addStatements = new List<LuauStatement>();
                AddToSize(addStatements, variantSize);
                var add = new Chunk(addStatements);
                if (head == null)
                    statements.Add(head = new IfStatement(condition, add, branches, null));
                else
                    branches.Add(new ElseIfBranch(condition, add));
            }

            return;
        }

        if (serializationField is MapField mapField)
        {
            AddToSize(statements, new NumberLiteral(mapField.LengthType.ByteCount()));

            // Counted here rather than by a pass of its own: a map has no length operator, and the write
            // needs the count for its prefix before the pairs go out.
            var countLocal = ReserveLocal(LeafName(mapField.Path) + "_measured");
            statements.Add(new LocalVariable(countLocal, null, Zero));

            var keyLocal = ReserveLocal(LeafName(mapField.Key.Path));
            var valueLocal = ReserveLocal(LeafName(mapField.Value.Path));
            var pairStatements = new List<LuauStatement>
            {
                new ExpressionStatement(new BinaryOperator(new Identifier(countLocal), "+=", One))
            };

            MeasureField(mapField.Key, new Identifier(keyLocal), pairStatements);
            MeasureField(mapField.Value, new Identifier(valueLocal), pairStatements);

            statements.Add(new ForStatement([keyLocal, valueLocal], value, new Chunk(pairStatements)));
            AddToSize(statements, ElementBitBlockSize(mapField.EntryBits, new Identifier(countLocal)));

            return;
        }

        if (serializationField is not ArrayField arrayField)
            return;

        AddToSize(statements, new NumberLiteral(arrayField.LengthType.ByteCount()));
        AddToSize(statements, ElementBitBlockSize(arrayField.Element.HeaderBits, Length(value)));

        // The element width varies per entry, so the only way to total it is to walk the value. A loop
        // per nesting level needs a counter of its own, or an inner one would clobber the outer's.
        var loop = ReserveLocal(LoopLocal);
        var elementValue = new ElementAccess(value, new Identifier(loop));
        var elementStatements = new List<LuauStatement>();
        MeasureField(arrayField.Element, elementValue, elementStatements);

        if (elementStatements.Count > 0)
            statements.Add(new NumericForStatement(loop, One, Length(value), null, new Chunk(elementStatements)));
    }

    /// <summary>Adds one field's width, inline when it can be stated as an expression.</summary>
    private void MeasureField(SerializationField serializationField, LuauExpression value, List<LuauStatement> statements)
    {
        if (InlineContribution(serializationField, value) is { } contribution)
        {
            AddToSize(statements, contribution);
            return;
        }

        if (serializationField.BodyBytes is { } fixedBytes)
        {
            if (fixedBytes > 0)
                AddToSize(statements, new NumberLiteral(fixedBytes));

            return;
        }

        EmitMeasure(serializationField, value, statements);
    }

    /// <summary>Adds to the running size, emitting nothing at all when the amount folds to zero.</summary>
    private static void AddToSize(List<LuauStatement> statements, LuauExpression amount)
    {
        if (amount is NumberLiteral { Value: 0 })
            return;

        statements.Add(new ExpressionStatement(new BinaryOperator(new Identifier(SizeLocal), "+=", amount)));
    }

    private static readonly List<string> CFrameSentinels = ["CFrame.identity"];

    /// <summary>Bytes a sentinelled field writes when nothing matched and its components go out in full.</summary>
    private static int SentinelComponentBytes(SerializationField serializationField) =>
        serializationField switch
        {
            DatatypeField datatypeField => datatypeField.Datatype.Components.Count * datatypeField.NumberType.ByteCount(),
            // Position components plus, for Compressed, the packed rotation now living in the body.
            CFrameField cframeField => cframeField.ComponentCount * cframeField.NumberType.ByteCount()
                + (cframeField.Encoding == CFrameEncoding.Compressed ? sizeof(uint) : 0),
            _ => 0
        };


    /// <summary>
    ///     Reserves the bit block a collection's entries share, returning its origin in bits, or null when
    ///     the entries need no bits at all. The block is claimed before any body so the bodies keep their
    ///     byte alignment.
    /// </summary>
    private Identifier? ReserveElementBits(int bitsPerElement, string leaf, LuauExpression count, Cursor cursor, List<LuauStatement> body)
    {
        if (bitsPerElement == 0)
            return null;

        var origin = ReserveLocal(leaf + "_bits");
        body.Add(new ConstVariable(origin, null, Multiply(cursor.Position, new NumberLiteral(8))));

        // Rounded up to whole bytes, since the bodies that follow are byte-addressed.
        var blockBytes = FloorDivide(
            new Parenthesized(Add(Multiply(count, new NumberLiteral(bitsPerElement)), new NumberLiteral(7))),
            new NumberLiteral(8)
        );

        cursor.AdvanceBy(body, blockBytes);
        return new Identifier(origin);
    }

    /// <summary>
    ///     Points the cursor at one entry's slice of the shared bit block, returning the undo. Bit offsets
    ///     restart per entry; byte offsets keep running, since bodies follow one another.
    /// </summary>
    private static Action EnterElement(Cursor cursor, LuauExpression? bitBase, int bitsPerElement, string loopLocal)
    {
        if (bitBase == null)
            return () => { };

        var previousBase = cursor.BitBase;
        var previousOffset = cursor.BitOffset;

        cursor.BitBase = Add(bitBase, Multiply(new Parenthesized(Subtract(new Identifier(loopLocal), One)), new NumberLiteral(bitsPerElement)));
        cursor.BitOffset = 0;

        return () =>
        {
            cursor.BitBase = previousBase;
            cursor.BitOffset = previousOffset;
        };
    }

    /// <summary>Whole bytes the shared bit block occupies, or zero when the entries need no bits.</summary>
    private static LuauExpression ElementBitBlockSize(int bitsPerElement, LuauExpression count) =>
        bitsPerElement == 0
            ? Zero
            : FloorDivide(
                new Parenthesized(Add(Multiply(count, new NumberLiteral(bitsPerElement)), new NumberLiteral(7))),
                new NumberLiteral(8)
            );

    private static BinaryOperator FloorDivide(LuauExpression left, LuauExpression right) => new BinaryOperator(left, "//", right);

    private static UnaryOperator Length(LuauExpression value) => new UnaryOperator("#", value);

    private static BinaryOperator IsPresent(LuauExpression value) => new BinaryOperator(value, "~=", new NilLiteral());

    /// <summary>
    ///     An all-zero-width type sends no buffer, and a type with no blob fields sends no blobs array.
    ///     Both are statically known here, so the absent field is simply omitted from the table.
    /// </summary>
    private Table BuildSerializedTable()
    {
        var initializers = new List<TableInitializer>();
        if (!schema.IsEmpty)
            initializers.Add(new PropertyTableInitializer("buffer", new Identifier(BufferLocal)));

        if (schema.HasBlobs)
            initializers.Add(new PropertyTableInitializer("blobs", new Identifier(BlobsLocal)));

        return new Table(initializers);
    }

    private void EmitWrite(SerializationField serializationField, LuauExpression source, Cursor cursor, List<LuauStatement> body) =>
        EmitValueWrite(serializationField, Access(source, serializationField.Path), cursor, body);

    /// <summary>
    ///     Writes a field given the expression holding its value, rather than resolving it from a path.
    ///     Array elements are reached by index, so they have no path of their own and reuse this directly.
    /// </summary>
    private void EmitValueWrite(SerializationField serializationField, LuauExpression value, Cursor cursor, List<LuauStatement> body)
    {
        switch (serializationField)
        {
            // Pinned by its type - the reader rebuilds it as a constant, so nothing goes on the wire.
            case ConstantField:
                return;

            case BoolField:
                body.Add(new ExpressionStatement(WriteBits(cursor, 1, new IfExpression(value, One, [], Zero))));
                return;

            case NumberField numberField:
                WriteNumber(cursor, numberField.NumberType, value, body);
                return;

            case RangedNumberField ranged:
            {
                // The common grid starts at zero and steps by one, where the shift and scale are both
                // identities - emitting them would cost a subtract and a divide on every write.
                var scaled = value;
                if (!ranged.Minimum.Equals(0d))
                    scaled = new Parenthesized(Subtract(scaled, new NumberLiteral(ranged.Minimum)));

                if (!ranged.Step.Equals(1d))
                    scaled = Divide(scaled, new NumberLiteral(ranged.Step));

                body.Add(new ExpressionStatement(WriteBits(cursor, ranged.HeaderBits, LuauFactory.MathCall("round", [scaled]))));
                return;
            }

            // Blobs cost zero buffer bytes: they are appended in schema order and consumed in the same
            // order, so position alone identifies them.
            case BlobField:
                body.Add(new ExpressionStatement(LuauFactory.TableCall("insert", [new Identifier(BlobsLocal), value])));
                return;

            case OptionalField optionalField:
            {
                body.Add(new ExpressionStatement(WriteBits(cursor, 1, new IfExpression(IsPresent(value), One, [], Zero))));

                // Offsets diverge here, so the cursor commits to a runtime position before the branch and
                // both paths advance the same local.
                cursor.GoDynamic(body);

                var present = new List<LuauStatement>();
                foreach (var (inner, innerValue) in ChildrenOf(optionalField, value))
                    EmitValueWrite(inner, innerValue, cursor, present);
                body.Add(new IfStatement(IsPresent(value), new Chunk(present), [], null));

                return;
            }

            case MapField mapField:
            {
                var leaf = LeafName(mapField.Path);

                // Counted here rather than reused from the measure: a map nested in an optional or a
                // collection has one count per occurrence, and the measure's local is scoped to the
                // branch or loop it was counted in.
                var count = new Identifier(ReserveLocal(leaf + "_written"));
                var counter = ReserveLocal(leaf + "_key");
                body.Add(new LocalVariable(count.Name, null, Zero));
                body.Add(
                    new ForStatement(
                        [counter],
                        value,
                        new Chunk([new ExpressionStatement(new BinaryOperator(count, "+=", One))])
                    )
                );

                WriteNumber(cursor, mapField.LengthType, count, body);
                cursor.GoDynamic(body);

                var pairBits = ReserveElementBits(mapField.EntryBits, leaf, count, cursor, body);
                var index = ReserveLocal(leaf + "_index");
                if (pairBits != null)
                    body.Add(new LocalVariable(index, null, One));

                var keyLocal = ReserveLocal(LeafName(mapField.Key.Path) + "_out");
                var valueLocal = ReserveLocal(LeafName(mapField.Value.Path) + "_out");
                var pairBody = new List<LuauStatement>();

                var restorePair = EnterElement(cursor, pairBits, mapField.EntryBits, index);
                EmitValueWrite(mapField.Key, new Identifier(keyLocal), cursor, pairBody);
                EmitValueWrite(mapField.Value, new Identifier(valueLocal), cursor, pairBody);
                restorePair();

                if (pairBits != null)
                    pairBody.Add(new ExpressionStatement(new BinaryOperator(new Identifier(index), "+=", One)));

                body.Add(new ForStatement([keyLocal, valueLocal], value, new Chunk(pairBody)));
                return;
            }

            case ArrayField arrayField:
            {
                var leaf = LeafName(arrayField.Path);
                var count = Length(value);
                WriteNumber(cursor, arrayField.LengthType, count, body);
                cursor.GoDynamic(body);

                // Entries needing header bits share a block reserved ahead of the bodies, so the bodies
                // stay byte-aligned and each entry gets a slice of its own rather than overwriting the
                // schema-wide header.
                var bitBase = ReserveElementBits(arrayField.Element.HeaderBits, leaf, count, cursor, body);

                var loop = ReserveLocal(LoopLocal);
                var elementBody = new List<LuauStatement>();
                var element = new Identifier(ReserveLocal(leaf + "_item"));
                elementBody.Add(new ConstVariable(element.Name, null, new ElementAccess(value, new Identifier(loop))));

                var restore = EnterElement(cursor, bitBase, arrayField.Element.HeaderBits, loop);
                EmitValueWrite(arrayField.Element, element, cursor, elementBody);
                restore();

                body.Add(new NumericForStatement(loop, One, count, null, new Chunk(elementBody)));
                return;
            }

            case StringField stringField:
            {
                // Bound once: the source would otherwise be indexed three times and measured twice, and
                // inside an array loop that cost is per element.
                var text = BindIfReused(value, 2, LeafName(stringField.Path), body);
                var length = new Identifier(ReserveLocal(LeafName(stringField.Path) + "_length"));
                body.Add(new ConstVariable(length.Name, null, Length(text)));

                WriteNumber(cursor, stringField.LengthType, length, body);
                cursor.GoDynamic(body);
                body.Add(new ExpressionStatement(BufferCall("writestring", [new Identifier(BufferLocal), cursor.Position, text])));
                cursor.AdvanceBy(body, length);
                return;
            }

            case DatatypeField datatypeField when !datatypeField.UseSentinels:
            {
                var bound = BindIfReused(value, datatypeField.Datatype.Components.Count, LeafName(datatypeField.Path), body);
                foreach (var component in datatypeField.Datatype.Components)
                    WriteNumber(cursor, datatypeField.NumberType, Access(bound, component), body);

                return;
            }

            case CFrameField { UseSentinels: false } cframeField:
                EmitCFrameWrite(cframeField, value, cursor, body);
                return;

            case DatatypeField or CFrameField:
            {
                var sentinels = SentinelNamesOf(serializationField)!;
                var indexLocal = new Identifier(SentinelIndexLocal(serializationField.Path));
                var bound = new Identifier(SentinelValueLocal(serializationField.Path));
                body.Add(new ExpressionStatement(WriteBits(cursor, BitWidth.ForStateCount(sentinels.Count + 1), indexLocal)));

                // Components are written only when nothing matched, so offsets past this point diverge.
                cursor.GoDynamic(body);

                var components = new List<LuauStatement>();
                if (serializationField is DatatypeField datatype)
                    foreach (var component in datatype.Datatype.Components)
                        WriteNumber(cursor, datatype.NumberType, Access(bound, component), components);
                else
                    EmitCFrameWrite((CFrameField)serializationField, bound, cursor, components);

                body.Add(new IfStatement(new BinaryOperator(indexLocal, "==", Zero), new Chunk(components), [], null));
                return;
            }

            // Resolved against the value handed in, not the parameter: inside an array the base is the
            // bound element, whose path segment ('leaves[]') is not a property that could be indexed.
            case TupleField tupleField:
                foreach (var (element, elementValue) in ChildrenOf(tupleField, value))
                    EmitValueWrite(element, elementValue, cursor, body);

                return;

            case UnionField unionField:
            {
                if (!_prologueTags.Contains(unionField.Path))
                    EmitUnionTag(unionField, value, body);

                var tag = new Identifier(UnionTagLocal(unionField.Path));
                body.Add(new ExpressionStatement(WriteBits(cursor, unionField.TagBits, tag)));
                cursor.GoDynamic(body);

                // Only one variant is live, so their header bits deliberately overlap - each branch
                // restarts from the same bit position and the widest one sizes the region.
                var startBit = cursor.BitOffset;
                var widestBit = startBit;
                var branches = new List<ElseIfBranch>();
                IfStatement? head = null;
                for (var index = 0; index < unionField.Variants.Count; index++)
                {
                    cursor.BitOffset = startBit;
                    var variantBody = new List<LuauStatement>();
                    foreach (var (variantField, variantValue) in ChildrenOf(unionField, unionField.Variants[index], value))
                        EmitValueWrite(variantField, variantValue, cursor, variantBody);

                    widestBit = Math.Max(widestBit, cursor.BitOffset);

                    // A literal union carries its whole value in the tag, so every variant writes nothing.
                    if (variantBody.Count == 0)
                        continue;

                    var condition = new BinaryOperator(tag, "==", new NumberLiteral(index));
                    if (head == null)
                        body.Add(head = new IfStatement(condition, new Chunk(variantBody), branches, null));
                    else
                        branches.Add(new ElseIfBranch(condition, new Chunk(variantBody)));
                }

                cursor.BitOffset = widestBit;
                return;
            }
        }
    }

    private void EmitCFrameWrite(CFrameField cframeField, LuauExpression value, Cursor cursor, List<LuauStatement> body)
    {
        foreach (var component in CFramePositionComponents)
            WriteNumber(cursor, cframeField.NumberType, Access(value, component), body);

        var quaternion = LuauFactory.RuntimeLibraryCall(["cframe_to_quaternion"], [value]);
        if (cframeField.Encoding == CFrameEncoding.Compressed)
        {
            // Exactly 32 bits either way; in the body it is a u32 so it can be skipped on a sentinel hit.
            if (cframeField.UseSentinels)
                WriteNumber(cursor, NumberType.U32, PackQuaternion(quaternion), body);
            else
                body.Add(new ExpressionStatement(WriteBits(cursor, CFrameEncodingExtensions.CompressedRotationBits, PackQuaternion(quaternion))));

            return;
        }

        body.Add(new MultiConstVariable(QuaternionLocals, quaternion));
        foreach (var local in QuaternionLocals)
            WriteNumber(cursor, cframeField.NumberType, new Identifier(local), body);
    }

    private static Call PackQuaternion(LuauExpression quaternion) => LuauFactory.RuntimeLibraryCall(["pack_quaternion"], [quaternion]);

    private void WriteNumber(Cursor cursor, NumberType numberType, LuauExpression value, List<LuauStatement> body)
    {
        body.Add(new ExpressionStatement(BufferCall("write" + numberType.BufferSuffix(), [new Identifier(BufferLocal), cursor.Position, value])));
        cursor.Advance(body, numberType.ByteCount());
    }

    private Call WriteBits(Cursor cursor, int bitCount, LuauExpression value)
    {
        var call = BufferCall(
            "writebits",
            [new Identifier(BufferLocal), cursor.BitPosition, new NumberLiteral(bitCount), value]
        );

        cursor.BitOffset += bitCount;
        return call;
    }
}
