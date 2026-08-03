namespace Loom.Core.TypeChecking.Serialization;

public static class BitWidth
{
    /// <summary>
    ///     Bits needed to distinguish <paramref name="stateCount" /> values. A single state needs no bits
    ///     at all - the value is already known statically, which is what lets single-variant unions and
    ///     literal-typed fields cost nothing on the wire.
    /// </summary>
    public static int ForStateCount(int stateCount)
    {
        if (stateCount <= 1)
            return 0;

        var bits = 0;
        var capacity = 1L;
        while (capacity < stateCount)
        {
            capacity <<= 1;
            bits++;
        }

        return bits;
    }

    public static int ToByteCount(int bitCount) => (bitCount + 7) / 8;
}
