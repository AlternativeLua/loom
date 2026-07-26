using Loom.Luau.AST;

namespace Loom.Core.Generation;

internal sealed class LuauState
{
    public LuauScope Scope = new();

    public Identifier PushToVariable(string name, LuauExpression expression, LuauType? type = null, bool isConst = true)
    {
        if (expression is Identifier identifier)
            return identifier;

        var id = Scope.AddIdentifier(name);
        Prereq(isConst ? new ConstVariable(id, type, expression) : new LocalVariable(id, type, expression));
        return new Identifier(id);
    }

    public (T Node, LuauScope Scope) Capture<T>(Func<T> callback)
    {
        T value = default!;
        var scope = CaptureScope(() => value = callback());
        return (value, scope);
    }

    private LuauScope CaptureScope(Action callback)
    {
        var captured = new LuauScope(Scope);
        Scope = captured;
        callback();
        Scope = Scope.Parent!;

        return captured;
    }

    public void Prereq(params LuauStatement[] statements) => Scope.PrereqStatements.AddRange(statements);
    public void Postreq(params LuauStatement[] statements) => Scope.PostreqStatements.AddRange(statements);
}