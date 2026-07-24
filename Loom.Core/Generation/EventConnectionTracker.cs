using Loom.Core.Resolving;
using Loom.Luau.AST;

namespace Loom.Core.Generation;

/// <summary>
/// Identifies the target of an event connection/disconnection: the event member itself, plus
/// (for interface-member events) which underlying instance it was accessed through, since the
/// same <see cref="PropertySymbol"/> is shared by every variable of that interface type.
/// </summary>
/// <param name="Instance">
/// <c>null</c> for global events (there is only ever one instance); the base variable's resolved
/// <see cref="Symbol"/> when the target is <c>identifier.event_name</c>; or a fresh <see cref="object"/>
/// when the base expression isn't a simple identifier (e.g. a call expression), which deliberately
/// makes the connection untrackable later since re-evaluating the base expression may yield a
/// different runtime object.
/// </param>
internal readonly record struct EventTarget(object? Instance, Symbol Event);

/// <summary>
/// Tracks event connections so a later '-=' can find the connection a '+=' produced. Connections
/// are kept in a hidden module-level table per event target (rather than a local variable), since
/// the connect and disconnect sites aren't guaranteed to share a Luau lexical scope - e.g. connecting
/// inside one function and disconnecting from another function, or from the top level.
/// </summary>
internal sealed class EventConnectionTracker
{
    private readonly Dictionary<EventTarget, Identifier> _stores = [];
    private readonly HashSet<(EventTarget Target, Symbol Function)> _connectedFunctions = [];

    public List<LuauStatement> StoreDeclarations { get; } = [];

    /// <summary>
    /// Returns the identifier of the hidden table backing connections for <paramref name="target"/>,
    /// declaring it (via <paramref name="allocateName"/>) the first time this target is seen.
    /// </summary>
    public Identifier GetOrCreateStore(EventTarget target, Func<string> allocateName)
    {
        if (_stores.TryGetValue(target, out var existing))
            return existing;

        var identifier = new Identifier(allocateName());
        _stores[target] = identifier;
        StoreDeclarations.Add(new LocalVariable(identifier.Name, null, Table.Empty));

        return identifier;
    }

    public void MarkConnected(EventTarget target, Symbol function) => _connectedFunctions.Add((target, function));
    public bool IsConnected(EventTarget target, Symbol function) => _connectedFunctions.Contains((target, function));
}
