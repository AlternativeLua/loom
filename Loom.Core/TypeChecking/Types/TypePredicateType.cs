namespace Loom.Core.TypeChecking.Types;

public sealed class TypePredicateType(int? parameterIndex, Type targetType) : PrimitiveType(PrimitiveTypeKind.Bool)
{
    public int? ParameterIndex { get; } = parameterIndex;
    public Type TargetType { get; } = targetType;

    public override int GetHashCode() => HashCode.Combine(typeof(TypePredicateType), ParameterIndex, TargetType);

    public override bool Equals(Type? other) =>
        other is TypePredicateType predicate && ParameterIndex == predicate.ParameterIndex && TargetType.Equals(predicate.TargetType);
}
