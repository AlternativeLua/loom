using System.Diagnostics.CodeAnalysis;
using Tomlyn.Serialization;

namespace Loom.Config;

public sealed class RealmConverter : TomlConverter<Realm>
{
    public override Realm Read(TomlReader reader)
    {
        var realmName = reader.GetString();
        return realmName.ToLowerInvariant() switch
        {
            "shared" => Realm.Shared,
            "client" => Realm.Client,
            "server" => Realm.Server,
            _ => throw reader.CreateException($"unknown realm '{realmName}'; expected 'shared', 'client' or 'server'.")
        };
    }

    [ExcludeFromCodeCoverage]
    public override void Write(TomlWriter writer, Realm value)
    {
        switch (value)
        {
            case Realm.Shared:
                writer.WriteStringValue("shared");
                break;
            case Realm.Client:
                writer.WriteStringValue("client");
                break;
            case Realm.Server:
                writer.WriteStringValue("server");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
