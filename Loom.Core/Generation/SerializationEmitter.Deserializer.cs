using Loom.Core.TypeChecking.Serialization;
using Loom.Luau;
using Loom.Luau.AST;

namespace Loom.Core.Generation;

/// <summary>Reading: bounds and tag validation, per-field reads, and rebuilding the value.</summary>
internal sealed partial class SerializationEmitter
{
    public Function EmitDeserializer()
    {
        _locals.Clear();
        var body = new List<LuauStatement>();
        var readCursor = new Cursor(schema.HeaderBytes);
        var reads = new List<LuauStatement>();
        var initializers = new List<TableInitializer>();

        if (!schema.IsEmpty)
        {
            body.Add(new ConstVariable(BufferLocal, null, new PropertyAccess(new Identifier(SerializedParameter), ["buffer"])));
            body.Add(EmitTruncationGuard());
        }

        if (schema.HasBlobs)
        {
            body.Add(new ConstVariable(BlobsLocal, null, new PropertyAccess(new Identifier(SerializedParameter), ["blobs"])));

            // A payload may omit the array entirely, and indexing nil would error rather than report.
            body.Add(
                new IfStatement(
                    new BinaryOperator(new Identifier(BlobsLocal), "==", new NilLiteral()),
                    new Chunk([new Return(BuildErrorTable("missing_blob", null, null))]),
                    [],
                    null
                )
            );

            body.Add(new LocalVariable(BlobIndexLocal, null, One));
        }

        foreach (var serializationField in schema.Fields)
            initializers.Add(new PropertyTableInitializer(LeafName(serializationField.Path), EmitRead(serializationField, readCursor, reads)));

        body.AddRange(reads);
        body.Add(
            new Return(
                new Table(
                    [
                        new PropertyTableInitializer("ok", new BooleanLiteral(true)),
                        new PropertyTableInitializer("value", new Table(initializers))
                    ]
                )
            )
        );

        return new Function(
            DeserializeName(schema.Interface.Name),
            null,
            [new Parameter(SerializedParameter, LuauFactory.QualifyRuntimeType(new TypeName("Serialized")))],
            LuauFactory.QualifyRuntimeType(
                new TypeName("Result", [new TypeName(schema.Interface.Name), LuauFactory.QualifyRuntimeType(new TypeName("DeserializeError"))])
            ),
            new Chunk(body)
        );
    }

    /// <summary>
    ///     The minimum width is known, so one up-front check covers every fixed read that follows - no
    ///     pcall, and a specific error rather than "buffer access out of bounds".
    /// </summary>
    private IfStatement EmitTruncationGuard()
    {
        var missing = new BinaryOperator(new Identifier(BufferLocal), "==", new NilLiteral());
        var tooShort = new BinaryOperator(BufferCall("len", [new Identifier(BufferLocal)]), "<", new NumberLiteral(MinimumByteCount()));
        return new IfStatement(
            new BinaryOperator(missing, "or", tooShort),
            new Chunk([new Return(BuildErrorTable("truncated", null, 0))]),
            [],
            null
        );
    }

    /// <summary>
    ///     Smallest buffer a valid payload can occupy - every fixed field at full width plus, for a
    ///     variable field, its unavoidable part. A string contributes its length prefix, since an empty
    ///     string still writes one. Enough to cover every read up to the first variable segment, which
    ///     then bounds-checks itself.
    /// </summary>
    private int MinimumByteCount() =>
        schema.HeaderBytes
        + schema.Fields.Sum(f => f.BodyBytes ?? MinimumOf(f));

    /// <summary>Unavoidable width of a variable field: a length prefix is written even when empty.</summary>
    private static int MinimumOf(SerializationField serializationField) =>
        serializationField switch
        {
            StringField stringField => stringField.LengthType.ByteCount(),
            ArrayField arrayField => arrayField.LengthType.ByteCount(),
            MapField mapField => mapField.LengthType.ByteCount(),
            _ => 0
        };

    private static Table BuildErrorTable(string kind, string? fieldPath, int? offset)
    {
        var initializers = new List<TableInitializer> { new PropertyTableInitializer("kind", new StringLiteral(kind)) };
        if (fieldPath != null)
            initializers.Add(new PropertyTableInitializer("field", new StringLiteral(fieldPath)));

        if (offset != null)
            initializers.Add(new PropertyTableInitializer("offset", new NumberLiteral(offset.Value)));

        return new Table(
            [
                new PropertyTableInitializer("ok", new BooleanLiteral(false)),
                new PropertyTableInitializer("error", new Table(initializers))
            ]
        );
    }

    /// <summary>
    ///     Returns the table initializers reconstructing this field, appending any statements it needs to
    ///     <paramref name="statements" />. Leaf reads are expressions, so most fields need no statement.
    /// </summary>
    /// <summary>
    ///     Reads one field, returning the expression that reconstructs it. A flattened struct builds its
    ///     own table from its parts, so nesting is structural rather than something a later pass has to
    ///     reassemble from dotted names.
    /// </summary>
    private LuauExpression EmitRead(SerializationField serializationField, Cursor cursor, List<LuauStatement> statements) =>
        serializationField switch
        {
            ConstantField constant => ToLiteral(constant.Value),
            BoolField => new BinaryOperator(ReadBits(cursor, 1), "==", One),
            NumberField numberField => ReadNumber(cursor, numberField.NumberType, statements),
            RangedNumberField ranged => EmitRangedRead(ranged, cursor),
            BlobField blobField => EmitBlobRead(blobField, statements),
            OptionalField optionalField => EmitOptionalRead(optionalField, cursor, statements),
            ArrayField arrayField => EmitArrayRead(arrayField, cursor, statements),
            MapField mapField => EmitMapRead(mapField, cursor, statements),
            StringField stringField => EmitStringRead(stringField, cursor, statements),
            DatatypeField { UseSentinels: false } datatypeField => new Call(
                new Identifier(datatypeField.Datatype.Constructor),
                datatypeField.Datatype.Components.Select(_ => ReadNumber(cursor, datatypeField.NumberType, statements)).ToList()
            ),
            CFrameField { UseSentinels: false } cframeField => EmitCFrameRead(cframeField, cursor, statements),
            DatatypeField or CFrameField => EmitSentinelRead(serializationField, cursor, statements),
            TupleField tupleField => EmitStructRead(tupleField, cursor, statements),
            UnionField unionField => EmitUnionRead(unionField, cursor, statements),
            _ => new NilLiteral()
        };

    /// <summary>Rebuilds a flattened struct as the table its properties belonged to.</summary>
    private Table EmitStructRead(TupleField tupleField, Cursor cursor, List<LuauStatement> statements)
    {
        var initializers = new List<TableInitializer>();
        foreach (var element in tupleField.Elements)
            initializers.Add(new PropertyTableInitializer(LeafName(element.Path), EmitRead(element, cursor, statements)));

        return new Table(initializers);
    }

    private LuauExpression EmitRangedRead(RangedNumberField ranged, Cursor cursor)
    {
        LuauExpression decoded = ReadBits(cursor, ranged.HeaderBits);
        if (!ranged.Step.Equals(1d))
            decoded = Multiply(decoded, new NumberLiteral(ranged.Step));

        return ranged.Minimum.Equals(0d) ? decoded : Add(new NumberLiteral(ranged.Minimum), decoded);
    }

    /// <summary>
    ///     Blobs are consumed positionally. The count is checked and the value type verified where it can
    ///     be - a wrong-typed blob from a hostile client would otherwise violate the declared type.
    /// </summary>
    private Identifier EmitBlobRead(BlobField blobField, List<LuauStatement> statements)
    {
        var slot = new ElementAccess(new Identifier(BlobsLocal), new Identifier(BlobIndexLocal));
        var local = ReserveLocal(LeafName(blobField.Path) + "_blob");
        statements.Add(new ConstVariable(local, null, slot));
        statements.Add(new ExpressionStatement(new BinaryOperator(new Identifier(BlobIndexLocal), "+=", One)));

        // A truncated blobs array and a wrong-typed one are different failures, so they are reported
        // separately rather than collapsed into a single guard.
        statements.Add(
            new IfStatement(
                new BinaryOperator(new Identifier(local), "==", new NilLiteral()),
                new Chunk([new Return(BuildErrorTable("missing_blob", blobField.Path, null))]),
                [],
                null
            )
        );

        // 'unknown' admits any value by definition, so there is nothing to check beyond presence.
        if (blobField.TypeofCheck == null)
            return new Identifier(local);

        LuauExpression condition = new BinaryOperator(
            new Call(new Identifier("typeof"), [new Identifier(local)]),
            "~=",
            new StringLiteral(blobField.TypeofCheck)
        );

        // typeof only proves it is an Instance; the declared class needs IsA on top.
        if (blobField.InstanceClass != null)
            condition = new BinaryOperator(
                condition,
                "or",
                new UnaryOperator("not ", new Call(new PropertyAccess(new Identifier(local), ["IsA"]), [new StringLiteral(blobField.InstanceClass)], true))
            );

        statements.Add(
            new IfStatement(
                condition,
                new Chunk([new Return(BuildErrorTable("invalid_blob", blobField.Path, null))]),
                [],
                null
            )
        );

        return new Identifier(local);
    }


    /// <summary>
    ///     Reads a count-prefixed run of pairs back into a table. Order is not preserved and does not need
    ///     to be - a map is unordered, so any order rebuilds the same value.
    /// </summary>
    private Identifier EmitMapRead(MapField mapField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = ReserveLocal(LeafName(mapField.Path));
        var countLocal = ReserveLocal(leaf + "_count");
        statements.Add(new ConstVariable(countLocal, null, ReadNumber(cursor, mapField.LengthType, statements)));
        cursor.GoDynamic(statements);

        var pairBits = ReserveElementBits(mapField.EntryBits, leaf, new Identifier(countLocal), cursor, statements);
        statements.Add(new ConstVariable(leaf, null, Table.Empty));

        var loop = ReserveLocal(LoopLocal);
        var pairBody = new List<LuauStatement>();
        var restorePair = EnterElement(cursor, pairBits, mapField.EntryBits, loop);
        var key = EmitRead(mapField.Key, cursor, pairBody);
        var value = EmitRead(mapField.Value, cursor, pairBody);
        restorePair();

        pairBody.Add(new ExpressionStatement(new BinaryOperator(new ElementAccess(new Identifier(leaf), key), "=", value)));
        statements.Add(new NumericForStatement(loop, One, new Identifier(countLocal), null, new Chunk(pairBody)));

        return new Identifier(leaf);
    }

    private Identifier EmitArrayRead(ArrayField arrayField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = ReserveLocal(LeafName(arrayField.Path));
        var countLocal = ReserveLocal(leaf + "_count");
        statements.Add(new ConstVariable(countLocal, null, ReadNumber(cursor, arrayField.LengthType, statements)));
        cursor.GoDynamic(statements);

        // Zero-width elements consume no buffer, so there is nothing for a bounds check to prove.
        if (arrayField.Element.BodyBytes is > 0 and { } elementBytes)
            statements.Add(
                new IfStatement(
                    new BinaryOperator(
                        BufferCall("len", [new Identifier(BufferLocal)]),
                        "<",
                        Add(new Identifier(OffsetLocal), Multiply(new Identifier(countLocal), new NumberLiteral(elementBytes)))
                    ),
                    new Chunk([new Return(BuildErrorTable("invalid_length", arrayField.Path, null))]),
                    [],
                    null
                )
            );

        // Claimed before the bodies, exactly as the writer laid it out.
        var bitBase = ReserveElementBits(arrayField.Element.HeaderBits, leaf, new Identifier(countLocal), cursor, statements);

        statements.Add(new ConstVariable(leaf, null, Table.Empty));

        var loop = ReserveLocal(LoopLocal);
        var elementBody = new List<LuauStatement>();
        var restore = EnterElement(cursor, bitBase, arrayField.Element.HeaderBits, loop);
        var element = EmitRead(arrayField.Element, cursor, elementBody);
        restore();

        elementBody.Add(
            new ExpressionStatement(new BinaryOperator(new ElementAccess(new Identifier(leaf), new Identifier(loop)), "=", element))
        );

        statements.Add(new NumericForStatement(loop, One, new Identifier(countLocal), null, new Chunk(elementBody)));
        return new Identifier(leaf);
    }

    /// <summary>
    ///     Guards a conditionally-present payload. The up-front minimum only covers what every payload
    ///     carries, so a branch the sender chose to take has to prove the bytes are actually there -
    ///     otherwise a truncated buffer throws out of the read instead of reporting.
    /// </summary>
    private void EmitBoundsGuard(List<LuauStatement> statements, int byteCount, string path)
    {
        if (byteCount <= 0)
            return;

        statements.Add(
            new IfStatement(
                new BinaryOperator(
                    BufferCall("len", [new Identifier(BufferLocal)]),
                    "<",
                    Add(new Identifier(OffsetLocal), new NumberLiteral(byteCount))
                ),
                new Chunk([new Return(BuildErrorTable("truncated", path, null))]),
                [],
                null
            )
        );
    }

    /// <summary>
    ///     Reads a variant tag and rebuilds the selected shape. A tag outside the declared variants
    ///     reports rather than producing a value the union never admitted.
    /// </summary>
    private Identifier EmitUnionRead(UnionField unionField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = ReserveLocal(LeafName(unionField.Path));
        var tagLocal = ReserveLocal(leaf + "_tag");
        statements.Add(new ConstVariable(tagLocal, null, ReadBits(cursor, unionField.TagBits)));
        statements.Add(new LocalVariable(leaf, null, new NilLiteral()));
        cursor.GoDynamic(statements);

        var startBit = cursor.BitOffset;
        var widestBit = startBit;
        var branches = new List<ElseIfBranch>();
        for (var index = 0; index < unionField.Variants.Count; index++)
        {
            cursor.BitOffset = startBit;
            var variant = unionField.Variants[index];
            var variantBody = new List<LuauStatement>();
            EmitBoundsGuard(variantBody, VariantBytes(variant), unionField.Path);

            var rebuilt = RebuildVariant(unionField, variant, cursor, variantBody);
            variantBody.Add(new ExpressionStatement(new BinaryOperator(new Identifier(leaf), "=", rebuilt)));
            widestBit = Math.Max(widestBit, cursor.BitOffset);

            var condition = new BinaryOperator(new Identifier(tagLocal), "==", new NumberLiteral(index));
            if (index == 0)
                statements.Add(
                    new IfStatement(
                        condition,
                        new Chunk(variantBody),
                        branches,
                        new Chunk([new Return(BuildErrorTable("invalid_tag", unionField.Path, null))])
                    )
                );
            else
                branches.Add(new ElseIfBranch(condition, new Chunk(variantBody)));
        }

        cursor.BitOffset = widestBit;
        return new Identifier(leaf);
    }

    /// <summary>
    ///     Reconstructs one variant. A literal union carries its whole value in the tag; a discriminated
    ///     one rebuilds the table, restoring the discriminant the tag already encoded.
    /// </summary>
    private LuauExpression RebuildVariant(UnionField unionField, SerializationVariant variant, Cursor cursor, List<LuauStatement> body)
    {
        if (unionField.Discrimination == UnionDiscrimination.LiteralValue)
            return ToLiteral(variant.Discriminant);

        if (unionField.Discrimination == UnionDiscrimination.RuntimeKind)
            return variant.Fields.Count == 1 ? EmitRead(variant.Fields[0], cursor, body) : new NilLiteral();

        var initializers = new List<TableInitializer>
        {
            new PropertyTableInitializer(unionField.DiscriminantName!, ToLiteral(variant.Discriminant))
        };

        foreach (var variantField in variant.Fields)
            initializers.Add(new PropertyTableInitializer(LeafName(variantField.Path), EmitRead(variantField, cursor, body)));

        return new Table(initializers);
    }

    /// <summary>
    ///     Reads a sentinel tag, rebuilding the well-known value it names or falling through to the
    ///     components. Reserved tags report rather than producing a value the type never allowed.
    /// </summary>
    private Identifier EmitSentinelRead(SerializationField serializationField, Cursor cursor, List<LuauStatement> statements)
    {
        var sentinels = SentinelNamesOf(serializationField)!;
        var leaf = ReserveLocal(LeafName(serializationField.Path));
        var indexLocal = ReserveLocal(leaf + "_sentinel");

        statements.Add(new ConstVariable(indexLocal, null, ReadBits(cursor, BitWidth.ForStateCount(sentinels.Count + 1))));
        statements.Add(new LocalVariable(leaf, null, new NilLiteral()));
        cursor.GoDynamic(statements);

        var componentBody = new List<LuauStatement>();
        EmitBoundsGuard(componentBody, SentinelComponentBytes(serializationField), serializationField.Path);

        var rebuilt = serializationField is DatatypeField datatypeField
            ? new Call(
                new Identifier(datatypeField.Datatype.Constructor),
                datatypeField.Datatype.Components.Select(_ => ReadNumber(cursor, datatypeField.NumberType, componentBody)).ToList()
            )
            : EmitCFrameRead((CFrameField)serializationField, cursor, componentBody);

        componentBody.Add(new ExpressionStatement(new BinaryOperator(new Identifier(leaf), "=", rebuilt)));

        var branches = new List<ElseIfBranch>();
        for (var index = 0; index < sentinels.Count; index++)
            branches.Add(
                new ElseIfBranch(
                    new BinaryOperator(new Identifier(indexLocal), "==", new NumberLiteral(index + 1)),
                    new Chunk([new ExpressionStatement(new BinaryOperator(new Identifier(leaf), "=", new Identifier(sentinels[index])))])
                )
            );

        statements.Add(
            new IfStatement(
                new BinaryOperator(new Identifier(indexLocal), "==", Zero),
                new Chunk(componentBody),
                branches,
                new Chunk([new Return(BuildErrorTable("invalid_tag", serializationField.Path, null))])
            )
        );

        return new Identifier(leaf);
    }

    /// <summary>
    ///     Reads a presence bit, then the payload only when it is set. The local starts nil so an absent
    ///     value needs no else branch.
    /// </summary>
    private Identifier EmitOptionalRead(OptionalField optionalField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = ReserveLocal(LeafName(optionalField.Path));
        var presentLocal = ReserveLocal(leaf + "_present");
        statements.Add(new ConstVariable(presentLocal, null, new BinaryOperator(ReadBits(cursor, 1), "==", One)));
        statements.Add(new LocalVariable(leaf, null, new NilLiteral()));
        cursor.GoDynamic(statements);

        var present = new List<LuauStatement>();
        EmitBoundsGuard(present, optionalField.Inner.BodyBytes ?? 0, optionalField.Path);

        // Read as one value, not as a list of initializers: a nested struct contributes one per
        // property, and assigning them in turn would leave the accumulator holding only the last.
        var inner = EmitRead(optionalField.Inner, cursor, present);
        present.Add(new ExpressionStatement(new BinaryOperator(new Identifier(leaf), "=", inner)));

        statements.Add(new IfStatement(new Identifier(presentLocal), new Chunk(present), [], null));
        return new Identifier(leaf);
    }

    /// <summary>
    ///     Reads a length-prefixed string, checking the prefix against what the buffer actually holds so a
    ///     hostile length reports rather than throwing out of the read.
    /// </summary>
    private Identifier EmitStringRead(StringField stringField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = ReserveLocal(LeafName(stringField.Path));
        var lengthLocal = ReserveLocal(leaf + "_length");
        statements.Add(new ConstVariable(lengthLocal, null, ReadNumber(cursor, stringField.LengthType, statements)));
        cursor.GoDynamic(statements);

        statements.Add(
            new IfStatement(
                new BinaryOperator(
                    BufferCall("len", [new Identifier(BufferLocal)]),
                    "<",
                    Add(new Identifier(OffsetLocal), new Identifier(lengthLocal))
                ),
                new Chunk([new Return(BuildErrorTable("invalid_length", stringField.Path, null))]),
                [],
                null
            )
        );

        var read = BufferCall("readstring", [new Identifier(BufferLocal), cursor.Position, new Identifier(lengthLocal)]);
        statements.Add(new ConstVariable(leaf, null, read));
        cursor.AdvanceBy(statements, new Identifier(lengthLocal));

        return new Identifier(leaf);
    }

    private Call EmitCFrameRead(CFrameField cframeField, Cursor cursor, List<LuauStatement> statements)
    {
        var arguments = CFramePositionComponents.ConvertAll(_ => ReadNumber(cursor, cframeField.NumberType, statements));
        if (cframeField.Encoding == CFrameEncoding.Compressed)
        {
            var packed = cframeField.UseSentinels
                ? ReadNumber(cursor, NumberType.U32, statements)
                : ReadBits(cursor, CFrameEncodingExtensions.CompressedRotationBits);

            arguments.Add(LuauFactory.RuntimeLibraryCall(["unpack_quaternion"], [packed]));
            return new Call(new Identifier("CFrame.new"), arguments);
        }

        for (var index = 0; index < 4; index++)
            arguments.Add(ReadNumber(cursor, cframeField.NumberType, statements));

        return new Call(new Identifier("CFrame.new"), arguments);
    }

    private LuauExpression ReadNumber(Cursor cursor, NumberType numberType, List<LuauStatement> statements)
    {
        var call = BufferCall("read" + numberType.BufferSuffix(), [new Identifier(BufferLocal), cursor.Position]);
        if (!cursor.IsDynamic)
        {
            cursor.Advance(statements, numberType.ByteCount());
            return call;
        }

        var local = ReserveTemporary();
        statements.Add(new ConstVariable(local, null, call));
        cursor.Advance(statements, numberType.ByteCount());

        return new Identifier(local);
    }

    private int _temporaries;
    private readonly HashSet<string> _locals = [];

    private string ReserveTemporary() => $"read_{_temporaries++}";

    /// <summary>
    ///     Claims a local name, suffixing until it is free. Several constructs name locals after the same
    ///     path leaf - a union's accumulator and the string read filling it, for instance - and the inner
    ///     binding would otherwise shadow the outer, leaving the assignment writing to itself.
    /// </summary>
    private string ReserveLocal(string preferred)
    {
        if (_locals.Add(preferred))
            return preferred;

        for (var suffix = 2;; suffix++)
            if (_locals.Add($"{preferred}_{suffix}"))
                return $"{preferred}_{suffix}";
    }

    private Call ReadBits(Cursor cursor, int bitCount)
    {
        var call = BufferCall("readbits", [new Identifier(BufferLocal), cursor.BitPosition, new NumberLiteral(bitCount)]);
        cursor.BitOffset += bitCount;
        return call;
    }
}
