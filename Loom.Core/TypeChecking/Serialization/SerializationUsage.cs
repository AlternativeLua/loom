namespace Loom.Core.TypeChecking.Serialization;

/// <summary>
///     Which of a <c>[serializable]</c> interface's codec pieces a file actually calls, collected before
///     generation so <c>EmitSerializers</c> can skip the rest. An unused <c>diff_binary</c>/
///     <c>apply_diff_binary</c> pair costs five generated functions nothing calls; an unused codec
///     object costs one table nothing reads.
/// </summary>
[Flags]
public enum SerializationUsage
{
    None = 0,
    Serialize = 1 << 0,
    Deserialize = 1 << 1,
    Delta = 1 << 2,
    SerializerObject = 1 << 3,
    All = Serialize | Deserialize | Delta | SerializerObject
}
