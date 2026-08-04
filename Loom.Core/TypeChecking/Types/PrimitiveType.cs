namespace Loom.Core.TypeChecking.Types;

public class PrimitiveType(PrimitiveTypeKind kind) : Type
{
    public static readonly PrimitiveType Number = new(PrimitiveTypeKind.Number);
    public static readonly PrimitiveType String = new(PrimitiveTypeKind.String);
    public static readonly PrimitiveType Bool = new(PrimitiveTypeKind.Bool);
    public static readonly PrimitiveType Void = new(PrimitiveTypeKind.Void);
    public static readonly PrimitiveType None = new(PrimitiveTypeKind.None);
    public static readonly PrimitiveType Unknown = new(PrimitiveTypeKind.Unknown);
    public static readonly PrimitiveType Never = new(PrimitiveTypeKind.Never);

    public PrimitiveTypeKind Kind { get; } = kind;

    public override int GetHashCode() => HashCode.Combine(typeof(PrimitiveType), Kind);
    public override bool Equals(Type? other) =>
        other is PrimitiveType primitive and not LiteralType and not TypePredicateType and not SizedNumberType && primitive.Kind == Kind;

    public override bool IsAssignableTo(Type other)
    {
        if (Kind == PrimitiveTypeKind.Never)
            return true;

        return other switch
        {
            LiteralType => false,
            PrimitiveType primitiveType => primitiveType.Kind == PrimitiveTypeKind.Unknown || IsKind(primitiveType.Kind),
            _ => base.IsAssignableTo(other)
        };
    }

    public override string ToString() => Kind.ToString().ToLower();

    private bool IsKind(PrimitiveTypeKind kind)
    {
        if (Kind is PrimitiveTypeKind.None or PrimitiveTypeKind.Void)
            return kind is PrimitiveTypeKind.None or PrimitiveTypeKind.Void;

        return Kind == kind;
    }
}