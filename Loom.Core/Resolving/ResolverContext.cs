namespace Loom.Core.Resolving;

public enum ResolverContext : byte
{
    None,
    Function,
    Loop,
    Scheduler,
    Ambient
}