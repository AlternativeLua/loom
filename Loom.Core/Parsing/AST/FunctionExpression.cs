using Loom.Core.Text;

namespace Loom.Core.Parsing.AST;

public class FunctionExpression(Token keyword, TypeParameters? typeParameters, Parameters? parameters, ColonTypeClause? returnType, Statement body)
    : Expression([keyword], [typeParameters, parameters, returnType, body]),
      IFunctionLike
{
    public Token Keyword { get; } = keyword;
    public TypeParameters? TypeParameters { get; } = typeParameters;
    public Parameters? Parameters { get; } = parameters;
    public ColonTypeClause? ReturnType { get; } = returnType;
    public Statement Body { get; } = body;

    public override T Accept<T>(Visitor<T> visitor) => visitor.VisitFunctionExpression(this);
}
