namespace Loom.Core.TypeChecking.Serialization;

/// <summary>
///     A Roblox datatype that decomposes into a fixed list of numeric components. Nothing in the type
///     system knows that a <c>Vector3</c> is three numbers named <c>X</c>/<c>Y</c>/<c>Z</c> rebuilt through
///     <c>Vector3.new</c>, so the serializer carries its own registry. <see cref="Components" /> and
///     <see cref="Sentinels" /> are Luau-side names, already in their <c>luau_name</c> form.
/// </summary>
/// <param name="Sentinels">
///     Well-known values that a <c>[packed]</c> interface encodes as a bit pattern instead of writing the
///     components. Order is load-bearing - the index becomes the wire tag, offset by one so that zero
///     stays reserved for "no sentinel, components follow".
/// </param>
public sealed record RobloxDatatype(
    string Name,
    IReadOnlyList<string> Components,
    string Constructor,
    IReadOnlyList<string> Sentinels,
    bool IsIntegral = false
)
{
    private static readonly RobloxDatatype[] _all =
    [
        new("Vector2", ["X", "Y"], "Vector2.new", ["Vector2.zero", "Vector2.one", "Vector2.xAxis", "Vector2.yAxis"]),
        new("Vector3", ["X", "Y", "Z"], "Vector3.new", ["Vector3.zero", "Vector3.one", "Vector3.xAxis", "Vector3.yAxis", "Vector3.zAxis"]),
        new("Vector2int16", ["X", "Y"], "Vector2int16.new", [], true),
        new("Vector3int16", ["X", "Y", "Z"], "Vector3int16.new", [], true),
        new("Color3", ["R", "G", "B"], "Color3.new", []),
        new("UDim", ["Scale", "Offset"], "UDim.new", []),
        new("UDim2", ["X.Scale", "X.Offset", "Y.Scale", "Y.Offset"], "UDim2.new", []),
        new("NumberRange", ["Min", "Max"], "NumberRange.new", []),
        new("Rect", ["Min.X", "Min.Y", "Max.X", "Max.Y"], "Rect.new", [])
    ];

    private static readonly Dictionary<string, RobloxDatatype> _byName = _all.ToDictionary(d => d.Name);

    /// <summary>
    ///     <c>CFrame</c> is deliberately absent from the registry - its rotation does not decompose into
    ///     independent components, so it is encoded by <see cref="CFrameEncoding" /> instead.
    /// </summary>
    public static bool TryGet(string name, out RobloxDatatype datatype) => _byName.TryGetValue(name, out datatype!);

    /// <summary>Bits needed to tag which sentinel matched, including the reserved "no sentinel" state.</summary>
    public int SentinelBits => Sentinels.Count == 0 ? 0 : BitWidth.ForStateCount(Sentinels.Count + 1);
}
