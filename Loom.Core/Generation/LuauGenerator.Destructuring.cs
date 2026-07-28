using Loom.Core.Parsing.AST;
using Loom.Luau;
using Loom.Luau.AST;
using TupleType = Loom.Core.TypeChecking.Types.TupleType;

namespace Loom.Core.Generation;

public sealed partial class LuauGenerator
{
    public override LuauNode VisitDestructuringDeclaration(DestructuringDeclaration destructuringDeclaration)
    {
        var initializerExpression = destructuringDeclaration.EqualsValueClause!.Value;
        if (destructuringDeclaration.Target is TupleDestructuringTarget tupleTarget)
            return EmitTupleDestructuring(tupleTarget, initializerExpression);

        var initializer = Visit(initializerExpression);
        var subject = _state.PushToVariable("_destructure", initializer);

        switch (destructuringDeclaration.Target)
        {
            case ArrayDestructuringTarget arrayTarget:
                for (var i = 0; i < arrayTarget.Elements.Count; i++)
                {
                    var name = arrayTarget.Elements[i].Name.Text;
                    _state.Scope.AddIdentifier(name);
                    _state.Prereq(new ConstVariable(name, null, new Luau.AST.ElementAccess(subject, new NumberLiteral(i + 1))));
                }

                break;

            case ObjectDestructuringTarget objectTarget:
                foreach (var field in objectTarget.Fields)
                {
                    var name = field.BindingName.Text;
                    _state.Scope.AddIdentifier(name);
                    _state.Prereq(new ConstVariable(name, null, new Luau.AST.PropertyAccess(subject, [field.Name.Text])));
                }

                break;
        }

        return new NoOpStatement();
    }

    /// <summary>
    ///     Tuple destructuring is kept out of the general array/object path above because it never needs a
    ///     table at all when the right-hand side already has its values in hand: a literal tuple binds each
    ///     name straight to its corresponding sub-expression, and a tuple-returning call already yields genuine
    ///     Luau multi-return values (whether that callee used the literal-return path or `table.unpack`
    ///     internally). Only a plain tuple-typed value (already living in a table) needs `table.unpack`.
    /// </summary>
    private LuauNode EmitTupleDestructuring(TupleDestructuringTarget target, Expression initializerExpression)
    {
        var names = target.Elements.ConvertAll(e => e.Name.Text);
        foreach (var name in names)
            _state.Scope.AddIdentifier(name);

        if (initializerExpression is TupleExpression tupleExpression)
        {
            for (var i = 0; i < names.Count; i++)
                _state.Prereq(new ConstVariable(names[i], null, Visit(tupleExpression.Expressions[i])));

            return new NoOpStatement();
        }

        var initializer = Visit(initializerExpression);
        if (initializerExpression is Invocation && _semanticModel.GetType(initializerExpression) is TupleType)
        {
            _state.Prereq(new MultiConstVariable(names, initializer));
            return new NoOpStatement();
        }

        var subject = _state.PushToVariable("_destructure", initializer);
        _state.Prereq(new MultiConstVariable(names, LuauFactory.TableCall("unpack", [subject])));
        return new NoOpStatement();
    }
}
