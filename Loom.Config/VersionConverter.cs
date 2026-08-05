using System.Diagnostics.CodeAnalysis;
using Tomlyn.Serialization;

namespace Loom.Config;

public sealed class VersionConverter : TomlConverter<Version>
{
    public override Version Read(TomlReader reader)
    {
        var text = reader.GetString();
        return Version.TryParse(text, out var version, out var error) ? version : throw reader.CreateException(error);
    }

    [ExcludeFromCodeCoverage]
    public override void Write(TomlWriter writer, Version value) => writer.WriteStringValue(value.ToString());
}
