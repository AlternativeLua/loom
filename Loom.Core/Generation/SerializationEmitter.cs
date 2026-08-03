using Loom.Core.TypeChecking.Serialization;
using Loom.Core.TypeChecking.Types;
using Loom.Luau;
using Loom.Luau.AST;

namespace Loom.Core.Generation;

/// <summary>
///     Turns a <see cref="SerializationSchema" /> into its pair of top-level Luau functions. Every offset
///     the schema pins at compile time becomes a literal, so a fixed-size type allocates one buffer of a
///     constant width and writes it with straight-line calls - there is no runtime schema to walk.
/// </summary>
/// <remarks>
///     Buffer library members are reached through file-level constants rather than the <c>buffer</c>
///     global, since a serializer touches them once per field on paths that run per frame. Which members
///     a file needs falls out of <see cref="BufferMembers" /> once every schema in it has been emitted.
/// </remarks>
internal sealed class SerializationEmitter(SerializationSchema schema, List<string> bufferMembers)
{
    private const string ValueParameter = "value";
    private const string SerializedParameter = "serialized";
    private const string BufferLocal = "b";
    private const string BlobsLocal = "blobs";
    private const string BlobIndexLocal = "blob_index";
    private const string OffsetLocal = "offset";
    private const string LoopLocal = "i";

    public static string SerializeName(string interfaceName) => $"{interfaceName}_serialize_binary";
    public static string DeserializeName(string interfaceName) => $"{interfaceName}_deserialize_binary";
    public static string SerializerName(string interfaceName) => $"{interfaceName}_serializer";
    public static string SerializerMapName(string interfaceName) => $"{interfaceName}_serializer_map";
    public static string BufferConstantName(string member) => $"buffer_{member}";

    /// <summary>
    ///     Emits one table per mapping interface, keyed exactly as the interface is. Properties key by
    ///     name; an indexer whose key is a literal type - the shape an enum-keyed map takes - keys by
    ///     that literal, so <c>[Message["ShootGun"]]: ShootGunPacket</c> lands under the member's value.
    /// </summary>
    public static ConstVariable? EmitSerializerMap(
        InterfaceType mapType,
        Func<InterfaceType, string?> resolveSerializerName)
    {
        var initializers = new List<TableInitializer>();
        foreach (var property in mapType.Properties)
        {
            if (property.ValueType is not InterfaceType valueType || resolveSerializerName(valueType) is not { } serializerName)
                continue;

            initializers.Add(new PropertyTableInitializer(property.Name, new Identifier(serializerName)));
        }

        if (mapType.Indexer is { KeyType: LiteralType key, ValueType: InterfaceType indexedValue }
            && resolveSerializerName(indexedValue) is { } indexedSerializer)
            initializers.Add(new ComputedPropertyTableInitializer(ToLiteral(key.Value), new Identifier(indexedSerializer)));

        return initializers.Count == 0
            ? null
            : new ConstVariable(SerializerMapName(mapType.Name), null, new Table(initializers));
    }

    /// <summary>
    ///     Bundles the pair into a value so it can be passed around, stored, or picked at runtime. The
    ///     named functions stay, so a call in the declaring file still goes direct - only crossing a
    ///     module boundary or holding the codec as a value pays the extra index.
    /// </summary>
    public ConstVariable EmitSerializerObject() =>
        new(
            SerializerName(schema.Interface.Name),
            LuauFactory.QualifyRuntimeType(new TypeName("Serializer", [new TypeName(schema.Interface.Name)])),
            new Table(
                [
                    new PropertyTableInitializer("serialize", new Identifier(SerializeName(schema.Interface.Name))),
                    new PropertyTableInitializer("deserialize", new Identifier(DeserializeName(schema.Interface.Name)))
                ]
            )
        );

    /// <summary>Declares the hoisted constants for the members used across a file, in first-use order.</summary>
    public static List<LuauStatement> DeclareBufferConstants(IEnumerable<string> members) =>
        members
            .Select(LuauStatement (member) => new ConstVariable(BufferConstantName(member), null, new PropertyAccess(new Identifier("buffer"), [member])))
            .ToList();

    private Identifier Buffer(string member)
    {
        if (!bufferMembers.Contains(member))
            bufferMembers.Add(member);

        return new Identifier(BufferConstantName(member));
    }

    private LuauExpression BufferCall(string member, List<LuauExpression> arguments) => new Call(Buffer(member), arguments);

    #region Serializer
    public Function EmitSerializer()
    {
        var body = new List<LuauStatement>();
        if (schema.HasBlobs)
            body.Add(new ConstVariable(BlobsLocal, null, Table.Empty));

        EmitSentinelPrologue(body);

        if (!schema.IsEmpty)
            body.Add(new ConstVariable(BufferLocal, null, BufferCall("create", [SizeExpression()])));

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

    /// <summary>
    ///     Total width. A fixed schema folds to a literal; variable-width fields add their runtime
    ///     contribution, so no separate measuring traversal is needed for widths expressible inline.
    /// </summary>
    private LuauExpression SizeExpression()
    {
        if (schema.FixedByteCount is { } fixedByteCount)
            return new NumberLiteral(fixedByteCount);

        var constant = schema.HeaderBytes + schema.Fields.Sum(f => f.BodyBytes ?? 0);
        LuauExpression size = new NumberLiteral(constant);
        foreach (var stringField in schema.Fields.OfType<StringField>())
            size = Add(size, Add(new NumberLiteral(stringField.LengthType.ByteCount()), Length(Access(new Identifier(ValueParameter), stringField.Path))));

        foreach (var array in schema.Fields.OfType<ArrayField>())
            size = Add(
                size,
                Add(
                    new NumberLiteral(array.LengthType.ByteCount()),
                    Multiply(Length(Access(new Identifier(ValueParameter), array.Path)), new NumberLiteral(array.Element.BodyBytes ?? 0))
                )
            );

        foreach (var serializationField in schema.Fields)
        {
            if (SentinelNamesOf(serializationField) is not { Count: > 0 })
                continue;

            size = Add(
                size,
                new IfExpression(
                    new BinaryOperator(new Identifier(SentinelIndexLocal(serializationField.Path)), "==", Zero),
                    new NumberLiteral(SentinelComponentBytes(serializationField)),
                    [],
                    Zero
                )
            );
        }

        foreach (var optional in schema.Fields.OfType<OptionalField>())
            size = Add(
                size,
                new IfExpression(IsPresent(Access(new Identifier(ValueParameter), optional.Path)), new NumberLiteral(optional.Inner.BodyBytes ?? 0), [], Zero)
            );

        return size;
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

    private static LuauExpression Length(LuauExpression value) => new UnaryOperator("#", value);

    private static LuauExpression IsPresent(LuauExpression value) => new BinaryOperator(value, "~=", new NilLiteral());

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

    private void EmitWrite(SerializationField serializationField, LuauExpression source, Cursor cursor, List<LuauStatement> body)
    {
        var value = Access(source, serializationField.Path);
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
                body.Add(
                    new ExpressionStatement(
                        WriteBits(
                            cursor,
                            ranged.HeaderBits,
                            LuauFactory.MathCall(
                                "round",
                                [Divide(new Parenthesized(Subtract(value, new NumberLiteral(ranged.Minimum))), new NumberLiteral(ranged.Step))]
                            )
                        )
                    )
                );

                return;

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
                EmitWrite(optionalField.Inner, source, cursor, present);
                body.Add(new IfStatement(IsPresent(value), new Chunk(present), [], null));

                return;
            }

            case ArrayField arrayField:
            {
                var count = Length(value);
                WriteNumber(cursor, arrayField.LengthType, count, body);
                cursor.GoDynamic(body);

                var elementBody = new List<LuauStatement>();
                EmitElementWrite(arrayField, new ElementAccess(value, new Identifier(LoopLocal)), cursor, elementBody);
                body.Add(new NumericForStatement(LoopLocal, One, count, null, new Chunk(elementBody)));

                return;
            }

            case StringField stringField:
            {
                var length = Length(value);
                WriteNumber(cursor, stringField.LengthType, length, body);
                cursor.GoDynamic(body);
                body.Add(new ExpressionStatement(BufferCall("writestring", [new Identifier(BufferLocal), cursor.Position, value])));
                cursor.AdvanceBy(body, length);
                return;
            }

            case DatatypeField datatypeField when !datatypeField.UseSentinels:
                foreach (var component in datatypeField.Datatype.Components)
                    WriteNumber(cursor, datatypeField.NumberType, Access(value, component), body);

                return;

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

            case TupleField tupleField:
                foreach (var element in tupleField.Elements)
                    EmitWrite(element, source, cursor, body);

                return;
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

    private static LuauExpression PackQuaternion(LuauExpression quaternion) => LuauFactory.RuntimeLibraryCall(["pack_quaternion"], [quaternion]);

    private void WriteNumber(Cursor cursor, NumberType numberType, LuauExpression value, List<LuauStatement> body)
    {
        body.Add(new ExpressionStatement(BufferCall("write" + numberType.BufferSuffix(), [new Identifier(BufferLocal), cursor.Position, value])));
        cursor.Advance(body, numberType.ByteCount());
    }

    private LuauExpression WriteBits(Cursor cursor, int bitCount, LuauExpression value)
    {
        var call = BufferCall(
            "writebits",
            [new Identifier(BufferLocal), new NumberLiteral(cursor.BitOffset), new NumberLiteral(bitCount), value]
        );

        cursor.BitOffset += bitCount;
        return call;
    }
    #endregion Serializer

    #region Deserializer
    public Function EmitDeserializer()
    {
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
            initializers.AddRange(EmitRead(serializationField, readCursor, reads));

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
    private List<TableInitializer> EmitRead(SerializationField serializationField, Cursor cursor, List<LuauStatement> statements)
    {
        var name = LeafName(serializationField.Path);
        switch (serializationField)
        {
            case ConstantField constant:
                return [new PropertyTableInitializer(name, ToLiteral(constant.Value))];

            case BoolField:
                return [new PropertyTableInitializer(name, new BinaryOperator(ReadBits(cursor, 1), "==", One))];

            case NumberField numberField:
                return [new PropertyTableInitializer(name, ReadNumber(cursor, numberField.NumberType, statements))];

            case RangedNumberField ranged:
                return
                [
                    new PropertyTableInitializer(
                        name,
                        Add(new NumberLiteral(ranged.Minimum), Multiply(ReadBits(cursor, ranged.HeaderBits), new NumberLiteral(ranged.Step)))
                    )
                ];

            case BlobField blobField:
                return [new PropertyTableInitializer(name, EmitBlobRead(blobField, statements))];

            case OptionalField optionalField:
                return [new PropertyTableInitializer(name, EmitOptionalRead(optionalField, cursor, statements))];

            case ArrayField arrayField:
                return [new PropertyTableInitializer(name, EmitArrayRead(arrayField, cursor, statements))];

            case StringField stringField:
                return [new PropertyTableInitializer(name, EmitStringRead(stringField, cursor, statements))];

            case DatatypeField { UseSentinels: false } datatypeField:
                return
                [
                    new PropertyTableInitializer(
                        name,
                        new Call(
                            new Identifier(datatypeField.Datatype.Constructor),
                            datatypeField.Datatype.Components.Select(_ => ReadNumber(cursor, datatypeField.NumberType, statements)).ToList()
                        )
                    )
                ];

            case CFrameField { UseSentinels: false } cframeField:
                return [new PropertyTableInitializer(name, EmitCFrameRead(cframeField, cursor, statements))];

            case DatatypeField or CFrameField:
                return [new PropertyTableInitializer(name, EmitSentinelRead(serializationField, cursor, statements))];

            case TupleField tupleField:
                return tupleField.Elements.SelectMany(element => EmitRead(element, cursor, statements)).ToList();

            default:
                return [];
        }
    }

    /// <summary>
    ///     Blobs are consumed positionally. The count is checked and the value type verified where it can
    ///     be - a wrong-typed blob from a hostile client would otherwise violate the declared type.
    /// </summary>
    private LuauExpression EmitBlobRead(BlobField blobField, List<LuauStatement> statements)
    {
        var slot = new ElementAccess(new Identifier(BlobsLocal), new Identifier(BlobIndexLocal));
        var local = LeafName(blobField.Path) + "_blob";
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
    ///     Elements of an array are written through their indexed access rather than a property path, so
    ///     the generic field walk cannot be reused directly.
    /// </summary>
    private void EmitElementWrite(ArrayField arrayField, LuauExpression element, Cursor cursor, List<LuauStatement> body)
    {
        if (arrayField.Element is NumberField numberField)
            WriteNumber(cursor, numberField.NumberType, element, body);
    }

    /// <summary>
    ///     Reads a length-prefixed array. The count is checked against the buffer before the loop, so a
    ///     hostile length reports rather than running off the end one element at a time.
    /// </summary>
    private LuauExpression EmitArrayRead(ArrayField arrayField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = LeafName(arrayField.Path);
        var countLocal = leaf + "_count";
        statements.Add(new ConstVariable(countLocal, null, ReadNumber(cursor, arrayField.LengthType, statements)));
        cursor.GoDynamic(statements);

        var elementBytes = arrayField.Element.BodyBytes ?? 0;
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

        statements.Add(new ConstVariable(leaf, null, Table.Empty));

        var elementBody = new List<LuauStatement>();
        if (arrayField.Element is NumberField numberField)
        {
            var read = ReadNumber(cursor, numberField.NumberType, elementBody);
            elementBody.Insert(
                elementBody.Count == 0 ? 0 : elementBody.Count - 1,
                new ExpressionStatement(
                    new BinaryOperator(new ElementAccess(new Identifier(leaf), new Identifier(LoopLocal)), "=", read)
                )
            );
        }

        statements.Add(new NumericForStatement(LoopLocal, One, new Identifier(countLocal), null, new Chunk(elementBody)));
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
    ///     Reads a sentinel tag, rebuilding the well-known value it names or falling through to the
    ///     components. Reserved tags report rather than producing a value the type never allowed.
    /// </summary>
    private LuauExpression EmitSentinelRead(SerializationField serializationField, Cursor cursor, List<LuauStatement> statements)
    {
        var sentinels = SentinelNamesOf(serializationField)!;
        var leaf = LeafName(serializationField.Path);
        var indexLocal = leaf + "_sentinel";

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
    private LuauExpression EmitOptionalRead(OptionalField optionalField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = LeafName(optionalField.Path);
        var presentLocal = leaf + "_present";
        statements.Add(new ConstVariable(presentLocal, null, new BinaryOperator(ReadBits(cursor, 1), "==", One)));
        statements.Add(new LocalVariable(leaf, null, new NilLiteral()));
        cursor.GoDynamic(statements);

        var present = new List<LuauStatement>();
        EmitBoundsGuard(present, optionalField.Inner.BodyBytes ?? 0, optionalField.Path);

        var initializers = EmitRead(optionalField.Inner, cursor, present);
        foreach (var initializer in initializers.OfType<PropertyTableInitializer>())
            present.Add(new ExpressionStatement(new BinaryOperator(new Identifier(leaf), "=", initializer.Value)));

        statements.Add(new IfStatement(new Identifier(presentLocal), new Chunk(present), [], null));
        return new Identifier(leaf);
    }

    /// <summary>
    ///     Reads a length-prefixed string, checking the prefix against what the buffer actually holds so a
    ///     hostile length reports rather than throwing out of the read.
    /// </summary>
    private LuauExpression EmitStringRead(StringField stringField, Cursor cursor, List<LuauStatement> statements)
    {
        var leaf = LeafName(stringField.Path);
        var lengthLocal = leaf + "_length";
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

    private LuauExpression EmitCFrameRead(CFrameField cframeField, Cursor cursor, List<LuauStatement> statements)
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

    private string ReserveTemporary() => $"read_{_temporaries++}";

    private LuauExpression ReadBits(Cursor cursor, int bitCount)
    {
        var call = BufferCall("readbits", [new Identifier(BufferLocal), new NumberLiteral(cursor.BitOffset), new NumberLiteral(bitCount)]);
        cursor.BitOffset += bitCount;
        return call;
    }
    #endregion Deserializer

    #region Helpers
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

    private static LuauExpression Access(LuauExpression source, string path) => new PropertyAccess(source, [..path.Split('.')]);

    private static string LeafName(string path) => path[(path.LastIndexOf('.') + 1)..];

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
    #endregion Helpers
}
