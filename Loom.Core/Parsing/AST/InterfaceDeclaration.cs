using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class InterfaceDeclaration(
    Token? sealedKeyword,
    Token keyword,
    Token name,
    TypeParameters? typeParameters,
    ColonTypeListClause? colonTypeListClause,
    InterfaceBody? body,
    Attributes? attributes = null
)
    : GenericNamedDeclaration(
        sealedKeyword != null ? [sealedKeyword] : [],
        keyword,
        name,
        typeParameters,
        colonTypeListClause,
        body,
        attributes
    ),
      IWithAttributes
{
    public Token? SealedKeyword { get; } = sealedKeyword;
    public ColonTypeListClause? ColonTypeListClause { get; } = colonTypeListClause;
    public InterfaceBody? Body { get; } = body;
    public Attributes? Attributes { get; } = attributes;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitInterfaceDeclaration(this);
}