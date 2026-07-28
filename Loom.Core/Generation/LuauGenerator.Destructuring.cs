using Loom.Core.Parsing.AST;
using Loom.Luau.AST;

namespace Loom.Core.Generation;

public sealed partial class LuauGenerator
{
    public override LuauNode VisitDestructuringDeclaration(DestructuringDeclaration destructuringDeclaration)
    {
        var initializer = Visit(destructuringDeclaration.EqualsValueClause!.Value);
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
}
