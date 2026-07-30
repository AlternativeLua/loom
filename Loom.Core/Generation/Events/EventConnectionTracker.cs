using System.Diagnostics.CodeAnalysis;
using Loom.Core.Resolving.Symbols;
using Loom.Luau.AST;

namespace Loom.Core.Generation.Events;

/// <summary>
///     Identifies the target of an event connection/disconnection: the event member itself, plus
///     (for interface-member events) which underlying instance it was accessed through, since the
///     same <see cref="PropertySymbol" /> is shared by every variable of that interface type.
/// </summary>
/// <param name="Instance">
///     <c>null</c> for global events (there is only ever one instance); the base variable's resolved
///     <see cref="Symbol" /> when the target is <c>identifier.event_name</c>; or a fresh <see cref="object" />
///     when the base expression isn't a simple identifier (e.g. a call expression), which deliberately
///     makes the connection untrackable later since re-evaluating the base expression may yield a
///     different runtime object.
/// </param>
internal readonly record struct EventTarget(object? Instance, Symbol Event);

/// <summary>
///     Tracks event connections so a later '-=' can find the connection a '+=' produced. Most connections
///     stay a plain local Luau variable, matching what '+=' would generate on its own. Only connections
///     whose '+=' and '-=' sites don't provably share a Luau lexical scope - e.g. connecting inside one
///     function and disconnecting from another function, or from the top level - fall back to a hidden
///     module-level table per event target, since a local wouldn't be visible at the disconnect site.
/// </summary>
internal sealed class EventConnectionTracker
{
    private readonly HashSet<(EventTarget Target, Symbol Function)> _connectedFunctions = [];
    private readonly Dictionary<(EventTarget Target, Symbol Function), Identifier> _localConnections = [];
    private readonly Dictionary<EventTarget, Identifier> _stores = [];

    public List<LuauStatement> StoreDeclarations { get; } = [];

    public Identifier GetOrCreateStore(EventTarget target, Func<(string Name, LuauExpression Value)> declare)
    {
        if (_stores.TryGetValue(target, out var existing))
            return existing;

        var (name, value) = declare();
        var identifier = new Identifier(name);
        _stores[target] = identifier;
        StoreDeclarations.Add(new ConstVariable(identifier.Name, null, value));

        return identifier;
    }

    public static string StoreExportName(string eventName) => $"_{eventName}_connections";

    public void TrackLocalConnection(EventTarget target, Symbol function, Identifier connectionVariable) =>
        _localConnections[(target, function)] = connectionVariable;

    public bool TryGetLocalConnection(EventTarget target, Symbol function, [MaybeNullWhen(false)] out Identifier connectionVariable) =>
        _localConnections.TryGetValue((target, function), out connectionVariable);

    public void MarkConnected(EventTarget target, Symbol function) => _connectedFunctions.Add((target, function));
    public bool IsConnected(EventTarget target, Symbol function) => _connectedFunctions.Contains((target, function));
}