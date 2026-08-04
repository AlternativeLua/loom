using System.Diagnostics.CodeAnalysis;
using Tomlyn.Serialization;

namespace Loom.Config;

public sealed class PackageNameConverter : TomlConverter<PackageName>
{
    public override PackageName Read(TomlReader reader)
    {
        var text = reader.GetString();
        return PackageName.TryParse(text, out var packageName, out var error) ? packageName : throw reader.CreateException(error);
    }

    [ExcludeFromCodeCoverage]
    public override void Write(TomlWriter writer, PackageName value) => writer.WriteStringValue(value.ToString());
}
