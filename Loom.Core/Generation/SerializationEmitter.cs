using Loom.Core.TypeChecking.Serialization;
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

    public static string SerializeName(string interfaceName) => $"{interfaceName}_serialize_binary";
    public static string DeserializeName(string interfaceName) => $"{interfaceName}_deserialize_binary";
    public static string SerializerName(string interfaceName) => $"{interfaceName}_serializer";
    public static string BufferConstantName(string member) => $"buffer_{member}";

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

        if (!schema.IsEmpty)
            body.Add(new ConstVariable(BufferLocal, null, BufferCall("create", [new NumberLiteral(schema.FixedByteCount!.Value)])));

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
                body.Add(new ExpressionStatement(WriteNumber(cursor, numberField.NumberType, value)));
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

            case DatatypeField datatypeField:
                foreach (var component in datatypeField.Datatype.Components)
                    body.Add(new ExpressionStatement(WriteNumber(cursor, datatypeField.NumberType, Access(value, component))));

                return;

            case CFrameField cframeField:
                EmitCFrameWrite(cframeField, value, cursor, body);
                return;

            case TupleField tupleField:
                foreach (var element in tupleField.Elements)
                    EmitWrite(element, source, cursor, body);

                return;
        }
    }

    private void EmitCFrameWrite(CFrameField cframeField, LuauExpression value, Cursor cursor, List<LuauStatement> body)
    {
        foreach (var component in CFramePositionComponents)
            body.Add(new ExpressionStatement(WriteNumber(cursor, cframeField.NumberType, Access(value, component))));

        var quaternion = LuauFactory.RuntimeLibraryCall(["cframe_to_quaternion"], [value]);
        if (cframeField.Encoding == CFrameEncoding.Compressed)
        {
            body.Add(new ExpressionStatement(WriteBits(cursor, CFrameEncodingExtensions.CompressedRotationBits, PackQuaternion(quaternion))));
            return;
        }

        body.Add(new MultiConstVariable(QuaternionLocals, quaternion));
        foreach (var local in QuaternionLocals)
            body.Add(new ExpressionStatement(WriteNumber(cursor, cframeField.NumberType, new Identifier(local))));
    }

    private static LuauExpression PackQuaternion(LuauExpression quaternion) => LuauFactory.RuntimeLibraryCall(["pack_quaternion"], [quaternion]);

    private LuauExpression WriteNumber(Cursor cursor, NumberType numberType, LuauExpression value)
    {
        var call = BufferCall(
            "write" + numberType.BufferSuffix(),
            [new Identifier(BufferLocal), new NumberLiteral(cursor.ByteOffset), value]
        );

        cursor.ByteOffset += numberType.ByteCount();
        return call;
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
        var tooShort = new BinaryOperator(BufferCall("len", [new Identifier(BufferLocal)]), "<", new NumberLiteral(schema.FixedByteCount!.Value));
        return new IfStatement(
            new BinaryOperator(missing, "or", tooShort),
            new Chunk([new Return(BuildErrorTable("truncated", null, 0))]),
            [],
            null
        );
    }

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
                return [new PropertyTableInitializer(name, ReadNumber(cursor, numberField.NumberType))];

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

            case DatatypeField datatypeField:
                return
                [
                    new PropertyTableInitializer(
                        name,
                        new Call(
                            new Identifier(datatypeField.Datatype.Constructor),
                            datatypeField.Datatype.Components.Select(_ => ReadNumber(cursor, datatypeField.NumberType)).ToList()
                        )
                    )
                ];

            case CFrameField cframeField:
                return [new PropertyTableInitializer(name, EmitCFrameRead(cframeField, cursor))];

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

    private LuauExpression EmitCFrameRead(CFrameField cframeField, Cursor cursor)
    {
        var arguments = CFramePositionComponents.ConvertAll(_ => ReadNumber(cursor, cframeField.NumberType));
        if (cframeField.Encoding == CFrameEncoding.Compressed)
        {
            arguments.Add(LuauFactory.RuntimeLibraryCall(["unpack_quaternion"], [ReadBits(cursor, CFrameEncodingExtensions.CompressedRotationBits)]));
            return new Call(new Identifier("CFrame.new"), arguments);
        }

        for (var index = 0; index < 4; index++)
            arguments.Add(ReadNumber(cursor, cframeField.NumberType));

        return new Call(new Identifier("CFrame.new"), arguments);
    }

    private LuauExpression ReadNumber(Cursor cursor, NumberType numberType)
    {
        var call = BufferCall("read" + numberType.BufferSuffix(), [new Identifier(BufferLocal), new NumberLiteral(cursor.ByteOffset)]);
        cursor.ByteOffset += numberType.ByteCount();
        return call;
    }

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
    private sealed class Cursor(int startingByteOffset)
    {
        public int BitOffset;
        public int ByteOffset = startingByteOffset;
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
