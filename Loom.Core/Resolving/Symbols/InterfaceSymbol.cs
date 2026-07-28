using Loom.Core.Parsing.AST;

namespace Loom.Core.Resolving.Symbols;

public sealed class InterfaceSymbol(InterfaceDeclaration declaration, string name, bool isSealed, List<InterfaceSymbol>? constraints)
    : Symbol(declaration, SymbolKind.Interface, name)
{
    public bool IsSealed { get; } = isSealed;
    public IReadOnlyList<InterfaceSymbol>? Constraints { get; } = constraints;
    public List<PropertySymbol> Properties { get; } = [];
    /// <summary> Current interface properties + all constraint properties </summary>
    public IReadOnlyList<PropertySymbol> FullProperties => field ??= Properties.Concat(GetFieldAndConstraintFields(i => i.Properties)).ToArray();
    public List<TraitSymbol> Implements { get; } = [];
    public IReadOnlyList<Implement> FullImplementations => field ??= Implementations.Concat(GetFieldAndConstraintFields(i => i.Implementations)).ToArray();
    public List<Implement> Implementations { get; } = [];

    public PropertySymbol? GetPropertyAtPath(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
            return null;

        var firstName = path[0];
        var property = FullProperties.FirstOrDefault(p => p.Name == firstName);
        return property is { PointsTo: { } pointsTo } && path.Count > 1
            ? pointsTo.GetPropertyAtPath(path.Skip(1).ToArray())
            : property;
    }

    public override string ToString() =>
        $"InterfaceSymbol({Name}, IsSealed: {IsSealed}, Properties: [{string.Join(", ", Properties.Select(s => s.Name))}] Implements: [{string.Join(", ", Implements.Select(s => s.Name))}], Constraints: [{string.Join(", ", Constraints?.Select(s => s.Name) ?? [])}])";

    private T[] GetFieldAndConstraintFields<T>(Func<InterfaceSymbol, IReadOnlyList<T>> selector) =>
        Constraints?.SelectMany(c => selector(c).Concat(c.GetFieldAndConstraintFields(selector))).ToArray() ?? [];
}