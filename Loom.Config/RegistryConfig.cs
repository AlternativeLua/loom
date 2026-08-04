using Tomlyn.Serialization;

namespace Loom.Config;

/// <summary>The <c>[registry]</c> table: where dependency specifiers are looked up.</summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RegistryConfig
{
    public const string DefaultIndex = "https://loom-lang.github.io/index";

    /// <summary>URL of the package index; a static index, so no server is required.</summary>
    [TomlPropertyName("index")] public string Index { get; set; } = DefaultIndex;
}
