namespace Loom.Core.TypeChecking.Serialization;

/// <summary>
///     Mirrors the intrinsic <c>CFrameType</c> enum in <c>Intrinsic/loom.loom</c>; member order matches the
///     Loom declaration because attribute arguments arrive as ordinals.
/// </summary>
public enum CFrameEncoding : byte
{
    /// <summary>
    ///     Position plus a smallest-three quaternion: the largest-magnitude component is dropped and
    ///     recovered from the unit-length constraint, leaving 2 index bits and 3 components at 10 bits
    ///     each - exactly 32 bits of rotation, with a worst-case angular error near 0.1 degrees.
    /// </summary>
    Compressed,

    /// <summary>Position plus all four quaternion components at the field's number type.</summary>
    Precise
}

public static class CFrameEncodingExtensions
{
    /// <summary>Rotation bits, excluding the position components which are sized by the field's number type.</summary>
    public static int RotationBits(this CFrameEncoding encoding, NumberType numberType) =>
        encoding == CFrameEncoding.Compressed
            ? CompressedRotationBits
            : numberType.ByteCount() * 8 * 4;

    /// <summary>2 bits for the dropped component index plus 10 bits for each of the three kept components.</summary>
    public const int CompressedRotationBits = 2 + CompressedComponentBits * 3;

    public const int CompressedComponentBits = 10;
}
