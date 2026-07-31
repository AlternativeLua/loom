namespace Loom.Core.Parsing.AST;

public interface IFunctionLike
{
    Parameters? Parameters { get; }
    ColonTypeClause? ReturnType { get; }
    Statement Body { get; }
}
