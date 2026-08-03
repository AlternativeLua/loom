namespace Loom.Core.TypeChecking.Serialization;

public static class BitWidth
{
    /// <summary>
    ///     Bits needed to distinguish <paramref name="stateCount" /> values. A single state needs no bits
    ///     at all - the value is already known statically, which is what lets single-variant unions and
    ///     literal-typed fields cost nothing on the wire.
    /// </summary>
    public static int ForStateCount(int stateCount) => ForStateCount((double)stateCount);

    /// <summary>
    ///     Takes the count as a double so a range spanning the whole of a 32-bit word is measured rather
    ///     than truncated - clamping it to <see cref="int.MaxValue" /> would report 31 and silently drop
    ///     the top half of every value.
    /// </summary>
    public static int ForStateCount(double stateCount)
    {
        if (stateCount <= 1)
            return 0;

        var bits = 0;
        var capacity = 1d;
        while (capacity < stateCount && bits < MaximumBits)
        {
            capacity *= 2;
            bits++;
        }

        return capacity < stateCount ? MaximumBits + 1 : bits;
    }

    /// <summary>Ceiling of <c>buffer.writebits</c>; a wider field has to go through a byte-aligned type.</summary>
    public const int MaximumBits = 32;

    public static int ToByteCount(int bitCount) => (bitCount + 7) / 8;
}
