using Loom.Core.Diagnostics;
using Loom.Core.Resolving.Symbols;
using Loom.Core.TypeChecking.Serialization;

namespace Loom.Testing;

[Collection("Assembly")]
public class SerializationSchemaTest
{
    private static SerializationSchema GetSchema(string source, string interfaceName = "MyData")
    {
        var (_, semanticModel, flowAnalyzer) = Utility.FlowAnalyze(source);
        var result = new Core.TypeChecking.TypeChecker(semanticModel, flowAnalyzer).Check();
        Utility.AssertNoErrors(result.Diagnostics);

        var schema = semanticModel.SerializationSchemas
            .FirstOrDefault(pair => pair.Key.Name == interfaceName)
            .Value;

        Assert.NotNull(schema);
        return schema;
    }

    #region AttributeMatrix
    [Fact]
    public void ThrowsFor_Packed_WithoutSerializable()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics("[packed] interface MyData { id: number }");
        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.MissingRequiredAttribute,
            "'packed' requires interface 'MyData' to also have the 'serializable' attribute.",
            "'packed' only changes how a serializable type is encoded."
        );
    }

    [Fact]
    public void ThrowsFor_PropertyAttribute_OnNonSerializableInterface()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            interface MyData {
                [number_type(NumberType.U8)]
                id: number;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.MissingRequiredAttribute,
            "'number_type' requires interface 'MyData' to have the 'serializable' attribute.",
            "add 'serializable' to 'MyData', or remove the attribute from 'id'."
        );
    }

    [Fact]
    public void ThrowsFor_NumberType_AndNumberRange_Together()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8), number_range(0, 100)]
                health: number;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.ConflictingAttributes,
            "'health' has both 'number_type' and 'number_range', which each set its width.",
            "use 'number_range' for a bounded value, or 'number_type' for a fixed width."
        );
    }

    [Fact]
    public void ThrowsFor_Quantize_WithoutNumberRange()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [number_step(0.01)]
                opacity: number;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.MissingRequiredAttribute,
            "'number_step' on 'opacity' requires 'number_range'.",
            "without bounds there is no bit width to derive from a step."
        );
    }

    [Fact]
    public void ThrowsFor_IgnoreSerialization_OnRequiredProperty()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [ignore_serialization]
                cached: string;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.InvalidAttributeTargetType,
            "'ignore_serialization' requires 'cached' to be optional, since there is no default value to restore.",
            "declare it as 'cached: string?'."
        );
    }

    [Fact]
    public void ThrowsFor_IgnoreSerialization_WithEncodingAttribute()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [ignore_serialization, number_type(NumberType.U8)]
                cached: number?;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.ConflictingAttributes,
            "'cached' is both ignored and annotated with 'number_type'.",
            "an ignored property is not encoded, so it cannot carry encoding attributes."
        );
    }

    [Fact]
    public void ThrowsFor_LengthType_WithSignedNumberType()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [length_type(NumberType.I16)]
                name: string;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.InvalidAttributeTargetType,
            "'length_type' on 'name' must be an unsigned number type, but is 'I16'.",
            "lengths are never negative; use U8, U16, or U32."
        );
    }

    [Fact]
    public void ThrowsFor_LengthType_OnNonLengthPrefixedProperty()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [length_type(NumberType.U16)]
                id: number;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.InvalidAttributeTargetType,
            "'length_type' requires 'id' to be a string or array, but it is 'number'."
        );
    }

    [Fact]
    public void ThrowsFor_NumberType_OnNonNumericProperty()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8)]
                name: string;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.InvalidAttributeTargetType,
            "'number_type' requires 'name' to have numeric components, but it is 'string'."
        );
    }

    [Fact]
    public void ThrowsFor_CFrameType_OnNonCFrameProperty()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [cframe_type(CFrameType.Precise)]
                position: Vector3;
            }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.InvalidAttributeTargetType,
            "'cframe_type' requires 'position' to be a CFrame, but it is 'Vector3'."
        );
    }

    [Fact]
    public void Allows_NumberType_OnRobloxDatatype()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface MyData {
                [number_type(NumberType.I16)]
                position: Vector3;
            }
            """
        );

        Utility.AssertNoErrors(diagnostics);
    }
    #endregion AttributeMatrix

    #region Encodability
    [Fact]
    public void ThrowsFor_NestedNonSerializableInterface()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            interface Inner { value: number }
            [serializable] interface MyData { inner: Inner }
            """
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.NotSerializable,
            "'inner' has type 'Inner', which is not serializable.",
            "add the 'serializable' attribute to interface 'Inner'."
        );
    }

    [Fact]
    public void ThrowsFor_RecursiveSerializableType()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics("[serializable] interface Node { next: Node }");
        Assert.NotNull(diagnostics.Find(d => d.Code == InternalCodes.RecursiveSerializableType));
    }

    [Fact]
    public void ThrowsFor_AmbiguousUnion()
    {
        var diagnostics = Utility.GetTypeCheckerDiagnostics(
            """
            [serializable] interface Circle { radius: number }
            [serializable] interface Square { side: number }
            [serializable] interface MyData { shape: Circle | Square }
            """
        );

        Assert.NotNull(diagnostics.Find(d => d.Code == InternalCodes.AmbiguousSerializableUnion));
    }
    #endregion Encodability

    #region Calls
    [Fact]
    public void ThrowsFor_SerializeBinary_OnNonSerializableInterface()
    {
        var diagnostics = Utility.GetGeneratorDiagnostics(
            """
            interface Plain { id: number }
            let value = new Plain { id: 1 };
            let payload = serialize_binary(value);
            """,
            true
        );

        Utility.AssertDiagnostic(
            diagnostics,
            InternalCodes.NotSerializable,
            "Interface 'Plain' is not serializable.",
            "add the 'serializable' attribute to 'Plain'."
        );
    }

    [Fact]
    public void ThrowsFor_DeserializeBinary_OnNonSerializableInterface()
    {
        var diagnostics = Utility.GetGeneratorDiagnostics(
            """
            interface Plain { id: number }
            let payload = none as never as Serialized;
            let restored = deserialize_binary::<Plain>(payload);
            """,
            true
        );

        Utility.AssertDiagnostic(diagnostics, InternalCodes.NotSerializable, "Interface 'Plain' is not serializable.");
    }
    [Fact]
    public void SerializesInterface_ImportedFromAnotherModule()
    {
        Utility.WithTempProject(
            [
                ("packets.loom",
                    """
                    [serializable]
                    export interface MyData {
                        [number_type(NumberType.U8)]
                        id: number;
                    }
                    """),
                ("main.server.loom",
                    """
                    import { MyData } from "./packets";
                    let payload = serialize_binary(new MyData { id: 3 });
                    let restored = deserialize_binary::<MyData>(payload);
                    """)
            ],
            (_, result) =>
            {
                Utility.AssertNoErrors(result.Diagnostics);

                var declaring = result.Files.Single(f => f.SourceFile.Name.Contains("packets")).RenderedLuau;
                var consumer = result.Files.Single(f => f.SourceFile.Name.Contains("main")).RenderedLuau;

                // The codec is emitted and exported once, and the consumer reaches it through the import
                // rather than through function names local to the declaring file.
                Assert.Contains("MyData_serializer = MyData_serializer", declaring);
                Assert.Contains("const MyData_serializer = packets.MyData_serializer", consumer);
                Assert.Contains("MyData_serializer.serialize(", consumer);
                Assert.Contains("MyData_serializer.deserialize(", consumer);
                Assert.DoesNotContain("MyData_serialize_binary(", consumer);
            }
        );
    }
    [Fact]
    public void ArrayOfStrings_MeasuresByWalkingTheValue()
    {
        var luau = Utility.GetLuauAST("[serializable] interface Names { values: string[] }", true).Render();

        // A variable-width element cannot state its width as an expression, so the total is accumulated
        // by walking the value before the buffer is allocated.
        Assert.Contains("local size = 0", luau);
        Assert.Contains("size += 4 + #value.values[i]", luau);
        Assert.Contains("buffer_create(size)", luau);

        // Element locals must not collide with the collection's own, nor carry brackets into a name.
        Assert.Contains("values_element", luau);
        Assert.DoesNotContain("values[]_", luau);
    }

    [Fact]
    public void ArrayOfUnmeasurableElements_IsGuarded()
    {
        // A bool lives in header bits, which sit at fixed positions in a header sized once for the whole
        // schema - every element would write over the same bits.
        var bits = Utility.GetGeneratorDiagnostics("[serializable] interface Flags { values: bool[] }", true);
        Assert.NotNull(bits.Find(d => d.Code == InternalCodes.NotImplemented));

        // A nested array has no width the enclosing measure can state either.
        var nested = Utility.GetGeneratorDiagnostics("[serializable] interface Grid { values: string[][] }", true);
        Assert.NotNull(nested.Find(d => d.Code == InternalCodes.NotImplemented));
    }

    [Fact]
    public void ArrayOfNumbers_EmitsLengthPrefixedLoop()
    {
        var luau = Utility.GetLuauAST(
                """
                [serializable] interface Scores {
                    [number_type(NumberType.U8)]
                    values: number[];
                }
                """,
                true
            )
            .Render();

        Assert.Contains("buffer_writeu32(b, 0, #value.values)", luau);
        Assert.Contains("for i = 1, #value.values do", luau);
        Assert.Contains("offset += 1", luau);

        // The count is bounds-checked before the loop rather than running off the end element by element.
        Assert.Contains("if buffer_len(b) < offset + values_count * 1 then", luau);
    }
    [Fact]
    public void PackedSentinel_SkipsComponentsOnMatch()
    {
        var luau = Utility.GetLuauAST(
                """
                [serializable, packed] interface Entity {
                    [number_type(NumberType.I16)]
                    position: Vector3;
                }
                """,
                true
            )
            .Render();

        // The sentinel resolves before the allocation, because a match writes no components at all.
        Assert.Contains("if position_value == Vector3.zero then", luau);
        Assert.Contains("buffer_create(1 + if position_sentinel == 0 then 6 else 0)", luau);
        Assert.Contains("if position_sentinel == 0 then", luau);

        // Reserved tags decode to nothing the type allows, so they report instead.
        Assert.Contains("kind = \"invalid_tag\"", luau);
    }

    [Fact]
    public void PackedCFrame_KeepsIdentityToASingleByte()
    {
        var luau = Utility.GetLuauAST(
                """
                [serializable, packed] interface Waypoint {
                    [number_type(NumberType.F32)]
                    frame: CFrame;
                }
                """,
                true
            )
            .Render();

        // Header bits are paid for unconditionally, so a sentinelled rotation moves to the body where it
        // rides behind the same conditional as the position - identity costs one byte, not five.
        Assert.Contains("buffer_create(1 + if frame_sentinel == 0 then 16 else 0)", luau);
        Assert.Contains("buffer_writeu32(b, offset, Loom.pack_quaternion(", luau);
    }

    [Fact]
    public void ConditionalPayload_IsBoundsCheckedBeforeReading()
    {
        var luau = Utility.GetLuauAST(
                """
                [serializable] interface Entity {
                    [number_type(NumberType.I16)]
                    target: number?;
                }
                """,
                true
            )
            .Render();

        // The up-front minimum only covers what every payload carries, so a branch the sender chose to
        // take has to prove its bytes are present rather than throwing out of the read.
        Assert.Contains("if buffer_len(b) < offset + 2 then", luau);
    }
    #region Unions
    private const string ActionUnion =
        """
        interface IAction<Kind: string> { kind: Kind }
        [serializable] interface LogOutAction: IAction<"LogOut">;
        [serializable] interface ClickAction: IAction<"Click"> {
            [number_type(NumberType.U8)]
            x: number;
        }

        [serializable] interface Envelope { action: LogOutAction | ClickAction }
        """;

    [Fact]
    public void DiscriminatedUnion_RestoresDiscriminantFromTheTag()
    {
        var luau = Utility.GetLuauAST(ActionUnion, true).Render();

        Assert.Contains("if action_value.kind == \"Click\" then", luau);
        Assert.Contains("buffer_writebits(b, 0, 1, action_tag)", luau);

        // The tag carries 'kind', so it costs nothing on the wire and is rebuilt on the way back.
        Assert.Contains("action = { kind = \"LogOut\" }", luau);
        Assert.Contains("action = { kind = \"Click\", x = ", luau);
        Assert.DoesNotContain("value.action.kind)", luau);
    }

    [Fact]
    public void DiscriminatedUnion_SizesPerVariant()
    {
        var luau = Utility.GetLuauAST(ActionUnion, true).Render();

        // The empty variant adds nothing, so only the one with a payload gets a branch.
        Assert.Contains("if action_tag == 1 then", luau);
        Assert.Contains("size += 1", luau);
        Assert.DoesNotContain("size += 0", luau);
    }

    [Fact]
    public void LiteralUnion_IsTagOnly()
    {
        var luau = Utility.GetLuauAST("[serializable] interface Paint { color: \"red\" | \"green\" | \"blue\" }", true).Render();

        // Three variants fit in two bits and the value is the tag, so nothing follows it.
        Assert.Contains("buffer_writebits(b, 0, 2, color_tag)", luau);
        Assert.Contains("color = \"red\"", luau);
        Assert.Contains("buffer_create(1)", luau);
    }

    [Fact]
    public void RuntimeKindUnion_DiscriminatesWithTypeof()
    {
        var luau = Utility.GetLuauAST("[serializable] interface Cell { content: number | string }", true).Render();

        Assert.Contains("if typeof(content_value) == \"string\" then", luau);

        // A variant carrying a string has to be measured, not just its fixed part, or the allocation
        // would be short before the variant's own writes began.
        Assert.Contains("size += 4 + #value.content", luau);
    }

    [Fact]
    public void Union_ReportsTagsOutsideTheDeclaredVariants()
    {
        var luau = Utility.GetLuauAST(ActionUnion, true).Render();
        Assert.Contains("kind = \"invalid_tag\", field = \"action\"", luau);
    }

    [Fact]
    public void Union_BoundsChecksVariantPayload()
    {
        var luau = Utility.GetLuauAST(ActionUnion, true).Render();

        // The minimum only covers the tag, so a variant the sender chose has to prove its bytes exist.
        Assert.Contains("if buffer_len(b) < offset + 1 then", luau);
    }

    [Fact]
    public void Union_ResolvesVariantsThatShadowIntrinsicNames()
    {
        // 'Path' is also a Roblox class, and resolving the intrinsic instead would leave a perfectly
        // serializable variant looking unserializable.
        var diagnostics = Utility.GetGeneratorDiagnostics(
            """
            interface IShape<Kind: string> { kind: Kind }
            [serializable] interface Dot: IShape<"Dot">;
            [serializable] interface Path: IShape<"Path"> {
                [number_type(NumberType.U8)]
                n: number;
            }

            [serializable] interface Drawing { shape: Dot | Path }
            """,
            true
        );

        Utility.AssertNoErrors(diagnostics);
    }

    [Fact]
    public void UnionOfUnmeasurableVariants_IsGuarded()
    {
        var diagnostics = Utility.GetGeneratorDiagnostics(
            """
            interface IShape<Kind: string> { kind: Kind }
            [serializable] interface Dot: IShape<"Dot">;
            [serializable] interface Sketch: IShape<"Sketch"> { points: bool[] }
            [serializable] interface Drawing { shape: Dot | Sketch }
            """,
            true
        );

        Assert.NotNull(diagnostics.Find(d => d.Code == InternalCodes.NotImplemented));
    }
    #endregion Unions

    #endregion Calls

    #region Layout
    [Fact]
    public void FixedSizeSchema_HasConstantByteCount()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8)]
                id: number;
                [number_type(NumberType.I16)]
                position: Vector3;
            }
            """
        );

        Assert.True(schema.IsFixedSize);
        Assert.Equal(0, schema.HeaderBits);
        Assert.Equal(7, schema.FixedByteCount);
        Assert.False(schema.HasBlobs);
    }

    [Fact]
    public void Packed_TradesFixedSizeForSentinelBits()
    {
        var schema = GetSchema(
            """
            [serializable, packed] interface MyData {
                [number_type(NumberType.U8)]
                id: number;
                [number_type(NumberType.I16)]
                position: Vector3;
            }
            """
        );

        // Five Vector3 sentinels plus the reserved "components follow" state need three bits, and the
        // components become conditional - so the type stops being fixed-size.
        Assert.Equal(3, schema.HeaderBits);
        Assert.False(schema.IsFixedSize);
    }

    [Fact]
    public void LiteralTypedProperty_CostsNothing()
    {
        var schema = GetSchema("[serializable] interface MyData { kind: \"spawn\" }");

        Assert.Equal(0, schema.HeaderBits);
        Assert.Equal(0, schema.FixedByteCount);
        Assert.True(schema.IsEmpty);
    }

    [Fact]
    public void BoolsPackIntoHeaderBits()
    {
        var schema = GetSchema("[serializable] interface MyData { a: bool; b: bool; c: bool }");

        Assert.Equal(3, schema.HeaderBits);
        Assert.Equal(1, schema.HeaderBytes);
        Assert.Equal(1, schema.FixedByteCount);
    }

    [Fact]
    public void OptionalAddsPresenceBit()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8)]
                id: number?;
            }
            """
        );

        Assert.Equal(1, schema.HeaderBits);
        Assert.False(schema.IsFixedSize);
    }

    [Fact]
    public void NumberDefaultsToF32()
    {
        var schema = GetSchema("[serializable] interface MyData { value: number }");

        var numberField = Assert.IsType<NumberField>(Assert.Single(schema.Fields));
        Assert.Equal(NumberType.F32, numberField.NumberType);
        Assert.Equal(4, schema.FixedByteCount);
    }

    [Fact]
    public void RangedNumberUsesExactBitWidth()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_range(0, 100)]
                health: number;
            }
            """
        );

        // 101 states fit in 7 bits, versus the 32 an unannotated f32 would spend.
        Assert.Equal(7, schema.HeaderBits);
        Assert.Equal(1, schema.FixedByteCount);
    }

    [Fact]
    public void QuantizeSetsGridSpacing()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_range(0, 1), number_step(0.01)]
                opacity: number;
            }
            """
        );

        Assert.Equal(7, schema.HeaderBits);
    }

    [Fact]
    public void BlobCostsNoBufferBytes()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                kind: "spawn";
                target: Instance;
            }
            """
        );

        Assert.True(schema.HasBlobs);
        Assert.True(schema.IsEmpty);
    }

    [Fact]
    public void BlobCarriesInstanceClassForChecking()
    {
        var schema = GetSchema("[serializable] interface MyData { part: Part }");

        var blob = Assert.IsType<BlobField>(Assert.Single(schema.Fields));
        Assert.Equal("Instance", blob.TypeofCheck);
        Assert.Equal("Part", blob.InstanceClass);
    }

    [Fact]
    public void IgnoredPropertyIsAbsentFromSchema()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8)]
                id: number;
                [ignore_serialization]
                cached: string?;
            }
            """
        );

        Assert.Equal("id", Assert.Single(schema.Fields).Path);
    }

    [Fact]
    public void NestedSerializableIsFlattenedIntoParentHeader()
    {
        var schema = GetSchema(
            """
            [serializable] interface Inner { a: bool; b: bool }
            [serializable] interface MyData { flag: bool; inner: Inner }
            """
        );

        // One shared header rather than a partial byte per nesting level.
        Assert.Equal(3, schema.HeaderBits);
        Assert.Equal(1, schema.FixedByteCount);
    }

    [Fact]
    public void LiteralUnionEncodesAsTagWithNoPayload()
    {
        var schema = GetSchema("[serializable] interface MyData { color: \"red\" | \"green\" | \"blue\" }");

        var union = Assert.IsType<UnionField>(Assert.Single(schema.Fields));
        Assert.Equal(UnionDiscrimination.LiteralValue, union.Discrimination);
        Assert.Equal(2, union.TagBits);
        Assert.Equal(0, schema.BodyBytes);
    }

    [Fact]
    public void DiscriminatedUnionDropsTheDiscriminantField()
    {
        var schema = GetSchema(
            """
            interface IAction<Kind: string> { kind: Kind }
            [serializable] interface LogOutAction: IAction<"LogOut">;
            [serializable] interface ClickAction: IAction<"Click"> {
                [number_type(NumberType.U8)]
                x: number;
            }

            [serializable] interface MyData { action: LogOutAction | ClickAction }
            """
        );

        var union = Assert.IsType<UnionField>(Assert.Single(schema.Fields));
        Assert.Equal(UnionDiscrimination.Discriminant, union.Discrimination);
        Assert.Equal("kind", union.DiscriminantName);
        Assert.Equal(1, union.TagBits);

        // The tag carries 'kind', so no variant re-encodes it.
        Assert.Empty(union.Variants[0].Fields);
        Assert.Equal("action.x", Assert.Single(union.Variants[1].Fields).Path);
    }

    [Fact]
    public void TupleWritesNoLengthPrefix()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8)]
                pair: (number, number);
            }
            """
        );

        Assert.True(schema.IsFixedSize);
        Assert.Equal(2, schema.FixedByteCount);
    }

    [Fact]
    public void ArrayIsVariableSized()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.U8)]
                ids: number[];
            }
            """
        );

        Assert.False(schema.IsFixedSize);
        var array = Assert.IsType<ArrayField>(Assert.Single(schema.Fields));
        Assert.Equal(NumberType.U32, array.LengthType);
    }

    [Fact]
    public void CFrameDefaultsToCompressed()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.F32)]
                frame: CFrame;
            }
            """
        );

        var frame = Assert.IsType<CFrameField>(Assert.Single(schema.Fields));
        Assert.Equal(CFrameEncoding.Compressed, frame.Encoding);

        // 32 bits of rotation plus a 12-byte position.
        Assert.Equal(32, schema.HeaderBits);
        Assert.Equal(16, schema.FixedByteCount);
    }

    [Fact]
    public void PreciseCFrameSpendsFourComponentsOnRotation()
    {
        var schema = GetSchema(
            """
            [serializable] interface MyData {
                [number_type(NumberType.F32), cframe_type(CFrameType.Precise)]
                frame: CFrame;
            }
            """
        );

        // Unlike Compressed, Precise writes four ordinary components next to the position rather than
        // packing the rotation into header bits - bit-writing a float would truncate it to an integer.
        Assert.Equal(0, schema.HeaderBits);
        Assert.Equal(28, schema.FixedByteCount);
    }
    #endregion Layout
}
