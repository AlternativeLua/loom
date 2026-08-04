using Loom.Core.TypeChecking.Serialization;

namespace Loom.Core.TypeChecking.Types;

/// <summary>
///     A <c>string</c> pinned to a length-prefix wire width - <c>string&lt;u8&gt;</c>. The third
///     <see cref="PrimitiveType" /> subclass alongside <see cref="LiteralType" /> and
///     <see cref="SizedNumberType" />, following the exact same shape: <see cref="PrimitiveType.Kind" />
///     stays <see cref="PrimitiveTypeKind.String" />, so nothing outside the serializer needs to know this
///     is anything other than a plain string.
/// </summary>
public sealed class SizedStringType(NumberType lengthType) : PrimitiveType(PrimitiveTypeKind.String)
{
    public NumberType LengthType { get; } = lengthType;

    public override int GetHashCode() => HashCode.Combine(typeof(SizedStringType), Kind, LengthType);
    public override bool Equals(Type? other) => other is SizedStringType sized && sized.LengthType == LengthType;

    public override string ToString() => $"string<{LengthType.ToString().ToLower()}>";
}
