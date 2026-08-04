namespace Loom.Core.TypeChecking.Serialization;

using Type = Types.Type;

/// <summary>
///     One encodable leaf or composite of a flattened serialization schema. A closed hierarchy kept in one
///     file because the generator switches over it exhaustively; unlike AST nodes and type kinds, these are
///     a compiler-internal IR with no visitor.
/// </summary>
/// <param name="Path">
///     Dotted access path from the value being serialized, e.g. <c>position</c> or <c>origin.frame</c>.
///     Used verbatim in diagnostics and as the <c>field</c> of a <c>DeserializeError</c>.
/// </param>
public abstract record SerializationField(string Path, Type ValueType)
{
    /// <summary>Bits this field claims in the schema-wide header written at offset zero.</summary>
    public virtual int HeaderBits => 0;

    /// <summary>
    ///     Bits a delta write reserves for this field: whatever flags it needs (changed, presence-changed,
    ///     tag-changed, length-changed) plus, where mutually exclusive branches share the same range, the
    ///     widest of what each branch needs - the same overlap a union's variants already share. A leaf
    ///     with no delta of its own contributes zero, same as <see cref="HeaderBits" /> for an unadorned field.
    /// </summary>
    public virtual int DiffHeaderBits => 0;

    /// <summary>Bytes claimed in the body, or <c>null</c> when the width depends on the runtime value.</summary>
    public virtual int? BodyBytes => 0;

    /// <summary>Whether serializing this field appends to the blobs array.</summary>
    public virtual bool ProducesBlob => false;

    public virtual IEnumerable<SerializationField> Children => [];
}

/// <summary>A literal-typed field. The value is pinned by the type, so it costs nothing and is rebuilt as a constant.</summary>
public sealed record ConstantField(string Path, Type ValueType, object? Value)
    : SerializationField(Path, ValueType);

/// <summary>A single bit in the header.</summary>
public sealed record BoolField(string Path, Type ValueType)
    : SerializationField(Path, ValueType)
{
    public override int HeaderBits => 1;
    public override int DiffHeaderBits => 1 + HeaderBits;
}

public sealed record NumberField(string Path, Type ValueType, NumberType NumberType)
    : SerializationField(Path, ValueType)
{
    public override int? BodyBytes => NumberType.ByteCount();
    public override int DiffHeaderBits => 1;
}

/// <summary>
///     A number confined to <c>[Minimum, Maximum]</c> on a grid of <see cref="Step" />, encoded as the
///     index into that grid. Sits in the header because the width is rarely a whole number of bytes.
/// </summary>
public sealed record RangedNumberField(string Path, Type ValueType, double Minimum, double Maximum, double Step)
    : SerializationField(Path, ValueType)
{
    public double StateCount => Math.Floor((Maximum - Minimum) / Step) + 1;
    public override int HeaderBits => BitWidth.ForStateCount(StateCount);
    public override int DiffHeaderBits => 1 + HeaderBits;
}

/// <summary>
///     A value that cannot live in a buffer - an Instance, a function, or an unconstrained <c>unknown</c>.
///     Costs zero buffer bytes: blobs are appended in schema order and consumed in the same order, so
///     position alone identifies them.
/// </summary>
public sealed record BlobField(string Path, Type ValueType, string? TypeofCheck, string? InstanceClass)
    : SerializationField(Path, ValueType)
{
    public override bool ProducesBlob => true;
    public override int DiffHeaderBits => 1;
}

public sealed record StringField(string Path, Type ValueType, NumberType LengthType)
    : SerializationField(Path, ValueType)
{
    public override int? BodyBytes => null;
    public override int DiffHeaderBits => 1;
}

public sealed record ArrayField(string Path, Type ValueType, NumberType LengthType, SerializationField Element)
    : SerializationField(Path, ValueType)
{
    public override int? BodyBytes => null;
    public override bool ProducesBlob => Element.ProducesBlob || Element.Children.Any(c => c.ProducesBlob);
    public override IEnumerable<SerializationField> Children => [Element];

    // The length-changed flag is the only bit this field owns; a full resend reuses the ordinary
    // write, and an unchanged-length diff shares a per-element block sized by the element's own
    // DiffHeaderBits, the same way HeaderBits already does for an array of header-bit-needing elements.
    public override int DiffHeaderBits => 1;
}

/// <summary>
///     A key-to-value map, written as a count-prefixed run of pairs. Unlike an array the entries have no
///     order the wire preserves, which is fine: a map is unordered by definition, so reading the pairs
///     back in any order rebuilds the same value.
/// </summary>
public sealed record MapField(string Path, Type ValueType, NumberType LengthType, SerializationField Key, SerializationField Value)
    : SerializationField(Path, ValueType)
{
    public override int? BodyBytes => null;
    public override bool ProducesBlob => Key.ProducesBlob || Value.ProducesBlob;
    public override IEnumerable<SerializationField> Children => [Key, Value];

    /// <summary>Bits one pair claims in the block the entries share.</summary>
    public int EntryBits => Key.HeaderBits + Value.HeaderBits;

    // A map's diff is entirely body-driven - added/removed/changed key runs, each length-prefixed -
    // so like HeaderBits it owns no bits of its own. A changed entry's value recurses through
    // Value's own diff, sharing a per-entry block sized by DiffEntryBits the same way EntryBits does.
    public int DiffEntryBits => Value.DiffHeaderBits;
}

/// <summary>A fixed-length sequence. The length is static, so unlike <see cref="ArrayField" /> it writes no prefix.</summary>
public sealed record TupleField(string Path, Type ValueType, IReadOnlyList<SerializationField> Elements)
    : SerializationField(Path, ValueType)
{
    public override int HeaderBits => Elements.Sum(e => e.HeaderBits);
    public override int? BodyBytes => Elements.Aggregate((int?)0, (total, e) => total + e.BodyBytes);
    public override bool ProducesBlob => Elements.Any(e => e.ProducesBlob);
    public override IEnumerable<SerializationField> Children => Elements;

    // No bit of its own - a flattened struct's fields are always present, so each one is diffed
    // independently rather than gated behind a presence check.
    public override int DiffHeaderBits => Elements.Sum(e => e.DiffHeaderBits);
}

/// <summary>One presence bit in the header, followed by the payload only when present.</summary>
public sealed record OptionalField(string Path, Type ValueType, SerializationField Inner)
    : SerializationField(Path, ValueType)
{
    public override int HeaderBits => 1 + Inner.HeaderBits;

    // Absent values write nothing, so a present-or-not payload is never a fixed width.
    public override int? BodyBytes => Inner.BodyBytes == 0 ? 0 : null;
    public override bool ProducesBlob => Inner.ProducesBlob;
    public override IEnumerable<SerializationField> Children => [Inner];

    // A presence-changed flag, then either a full resend (HeaderBits, presence plus a fresh inner
    // value) or a recursive dive into Inner's own diff - mutually exclusive, so they share the same
    // range the way a union's variants already do, rather than paying for both.
    public override int DiffHeaderBits => 1 + Math.Max(HeaderBits, Inner.DiffHeaderBits);
}

public sealed record SerializationVariant(object? Discriminant, IReadOnlyList<SerializationField> Fields);

/// <summary>
///     A union encoded as a tag plus the selected variant's payload. Every variant must be distinguishable
///     at serialize time - by literal value, by <c>typeof</c>, or by a shared literal discriminant field.
/// </summary>
public sealed record UnionField(string Path, Type ValueType, UnionDiscrimination Discrimination, string? DiscriminantName, IReadOnlyList<SerializationVariant> Variants)
    : SerializationField(Path, ValueType)
{
    public int TagBits => BitWidth.ForStateCount(Variants.Count);
    public override int HeaderBits => TagBits + Variants.Max(v => v.Fields.Sum(f => f.HeaderBits));

    public override int? BodyBytes =>
        Variants.Count == 0
            ? 0
            : Variants
                .Select(v => v.Fields.Aggregate((int?)0, (total, f) => total + f.BodyBytes))
                .Distinct()
                .ToList() is [var only]
                ? only
                : null;

    public override bool ProducesBlob => Variants.Any(v => v.Fields.Any(f => f.ProducesBlob));
    public override IEnumerable<SerializationField> Children => Variants.SelectMany(v => v.Fields);

    // A tag-changed flag, then either a full resend of the new variant (HeaderBits) or a recursive
    // dive into the shared, unchanged variant's fields - mutually exclusive, so they overlap the same
    // way the ordinary write already lets live variants share one region.
    public override int DiffHeaderBits => 1 + Math.Max(HeaderBits, Variants.Max(v => v.Fields.Sum(f => f.DiffHeaderBits)));
}

public enum UnionDiscrimination : byte
{
    /// <summary>Every variant is a literal, so the tag is the whole value.</summary>
    LiteralValue,

    /// <summary>Variants have distinct runtime kinds, told apart with <c>typeof</c>.</summary>
    RuntimeKind,

    /// <summary>Variants are interfaces sharing a literal-typed field whose value picks the variant.</summary>
    Discriminant
}

/// <summary>A Roblox datatype decomposed into numeric components, optionally sentinel-tagged under <c>[packed]</c>.</summary>
public sealed record DatatypeField(string Path, Type ValueType, RobloxDatatype Datatype, NumberType NumberType, bool UseSentinels)
    : SerializationField(Path, ValueType)
{
    public override int HeaderBits => UseSentinels ? Datatype.SentinelBits : 0;

    // A sentinel match writes no components at all, which is what costs the type its fixed width.
    public override int? BodyBytes => UseSentinels ? null : Datatype.Components.Count * NumberType.ByteCount();
    public override int DiffHeaderBits => 1 + HeaderBits;
}

public sealed record CFrameField(string Path, Type ValueType, CFrameEncoding Encoding, NumberType NumberType, bool UseSentinels)
    : SerializationField(Path, ValueType)
{
    public const int SentinelStateCount = 2;

    // Compressed packs the whole rotation into header bits; Precise writes four ordinary components
    // alongside the position, since bit-writing a float would truncate it to an integer.
    //
    // Under sentinels the rotation moves to the body instead. Header bits sit at fixed positions and so
    // are always paid for, which would leave a sentinelled identity costing the full 32 bits it exists
    // to avoid - in the body they ride behind the same conditional as the position.
    public override int HeaderBits =>
        (Encoding == CFrameEncoding.Compressed && !UseSentinels ? CFrameEncodingExtensions.CompressedRotationBits : 0)
        + (UseSentinels ? BitWidth.ForStateCount(SentinelStateCount) : 0);

    public int ComponentCount => Encoding == CFrameEncoding.Compressed ? 3 : 7;

    public override int? BodyBytes => UseSentinels ? null : ComponentCount * NumberType.ByteCount();
    public override int DiffHeaderBits => 1 + HeaderBits;
}
