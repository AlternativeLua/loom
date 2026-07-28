namespace Loom.Core.TypeChecking.Types;

/// <summary>
///     The intrinsic <c>Tuple</c> generic constraint marker - matches any concrete <see cref="TupleType" />
///     but has no structural shape of its own, so it can't be satisfied by an ordinary object/array the way
///     an empty interface would be.
/// </summary>
public sealed class TupleMarkerType : Type
{
    public override bool Equals(Type? other) => other is TupleMarkerType;
    public override int GetHashCode() => HashCode.Combine(typeof(TupleMarkerType));
    public override bool IsAssignableTo(Type other) => other is TupleMarkerType || base.IsAssignableTo(other);
    public override string ToString() => "Tuple";
}
