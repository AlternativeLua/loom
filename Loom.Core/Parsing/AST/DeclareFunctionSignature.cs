using System.Diagnostics.CodeAnalysis;
using Loom.Core.Resolving;
using Loom.Core.Resolving.Symbols;
using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class DeclareFunctionSignature(
    Token keyword,
    Token name,
    TypeParameters? typeParameters,
    Parameters? parameters,
    ColonTypeClause returnType,
    Attributes? attributes = null,
    params Node?[] extraChildren
)
    : GenericNamedDeclaration([], keyword, name, typeParameters, [parameters, returnType, attributes, ..extraChildren]),
      IWithAttributes
{
    public Parameters? Parameters { get; } = parameters;
    public ColonTypeClause ReturnType { get; } = returnType;
    public Attributes? Attributes { get; } = attributes;

    public bool TryGetIntrinsicAttribute(SemanticModel semanticModel, string name, [MaybeNullWhen(false)] out AttributeSymbol attribute)
    {
        var functionSymbol = semanticModel.GetDeclarationSymbol(this, SymbolKind.Function) as PropertySymbol;
        attribute = functionSymbol?.Attributes.Find(a => a is { IsIntrinsic: true } && a.Name == name);
        return attribute != null;
    }

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitDeclareFunctionSignature(this);
}
