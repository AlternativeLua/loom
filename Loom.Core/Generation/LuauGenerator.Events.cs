using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving;
using Loom.Core.Text;
using Loom.Luau;
using Loom.Luau.AST;
using BinaryOperator = Loom.Luau.AST.BinaryOperator;
using PropertyAccess = Loom.Core.Parsing.AST.PropertyAccess;
using ExpressionStatement = Loom.Core.Parsing.AST.ExpressionStatement;
using Identifier = Loom.Core.Parsing.AST.Identifier;
using QualifiedName = Loom.Core.Parsing.AST.QualifiedName;
using TypeName = Loom.Luau.AST.TypeName;

namespace Loom.Core.Generation;

public sealed partial class LuauGenerator
{
    public override LuauNode VisitEventDeclaration(EventDeclaration eventDeclaration)
    {
        // TODO: generic events
        if (eventDeclaration.TypeParameters != null)
            _diagnostics.NotImplemented(eventDeclaration.TypeParameters, "Generic event declarations are not yet supported.");

        _semanticModel.RuntimeReferences += 2;
        var parameterTypes = eventDeclaration.Parameters?.ParameterList.ConvertAll(p => Visit(p.ColonTypeClause!.Type)) ?? [];
        var eventType = LuauFactory.QualifyRuntimeType(new TypeName("Event", parameterTypes));
        return new ConstVariable(eventDeclaration.Name.Text, eventType, LuauFactory.RuntimeLibraryCall(["Event", "new"], []));
    }

    public override LuauNode VisitAssignmentOperator(AssignmentOperator assignmentOperator)
    {
        if (assignmentOperator.Operator.Kind is SyntaxKind.PlusEquals or SyntaxKind.MinusEquals
            && ResolveEventTarget(assignmentOperator.Left) is { } eventTarget)
        {
            return GenerateEventAssignment(assignmentOperator, eventTarget);
        }

        if (assignmentOperator.Parent is ExpressionStatement)
            return VisitBinaryOperator(assignmentOperator);

        if (assignmentOperator.Left is Identifier)
        {
            var binary = (BinaryOperator)VisitBinaryOperator(assignmentOperator);
            var assignmentStatement = new Luau.AST.ExpressionStatement(binary);
            _state.Prereq(assignmentStatement);

            return binary.Left;
        }

        var left = Visit(assignmentOperator.Left);
        var right = Visit(assignmentOperator.Right);
        if (assignmentOperator.Parent is EqualsValueClause { Parent: NamedDeclaration declaration })
        {
            var identifierAssignment = new BinaryOperator(left, "=", new Luau.AST.Identifier(declaration.Name.Text));
            _state.Postreq(new Luau.AST.ExpressionStatement(identifierAssignment));

            return right;
        }

        var assigned = _state.PushToVariable("_assigned", right);
        var boundAssignment = new BinaryOperator(left, "=", assigned);
        _state.Prereq(new Luau.AST.ExpressionStatement(boundAssignment));

        return assigned;
    }

    private EventTarget? ResolveEventTarget(Expression left)
    {
        if (_semanticModel.GetSymbol(left) is { Kind: SymbolKind.Event } globalEventSymbol)
            return new EventTarget(null, globalEventSymbol);

        if (_semanticModel.GetPropertySymbol(left) is not { Kind: SymbolKind.Event } propertySymbol)
            return null;

        return new EventTarget(GetInstanceKey(left), propertySymbol);
    }

    private object? GetInstanceKey(Expression left) => left switch
    {
        PropertyAccess { Expression: Identifier identifier } => _semanticModel.GetSymbol(identifier),
        QualifiedName { Identifier: var identifier } => _semanticModel.GetSymbol(identifier),
        _ => new object()
    };

    private LuauExpression GenerateEventAssignment(AssignmentOperator assignmentOperator, EventTarget eventTarget)
    {
        var connectionTarget = Visit(assignmentOperator.Left);
        return assignmentOperator.Operator.Kind == SyntaxKind.PlusEquals
            ? GenerateEventConnect(assignmentOperator, connectionTarget, eventTarget)
            : GenerateEventDisconnect(assignmentOperator, eventTarget);
    }

    private LuauExpression GenerateEventConnect(AssignmentOperator assignmentOperator, LuauExpression connectionTarget, EventTarget eventTarget)
    {
        var function = assignmentOperator.Right;
        var luauFunction = WrapAnonymousFunction(function, Visit(function), new UnitType());
        var connect = new Call(new Luau.AST.PropertyAccess(connectionTarget, ["Connect"]), [luauFunction], true);
        if (luauFunction is AnonymousFunction || function is not Identifier identifier || _semanticModel.GetSymbol(identifier) is not { } functionSymbol)
            return connect;

        var store = GetConnectionStore(eventTarget);
        _eventConnections.MarkConnected(eventTarget, functionSymbol);

        var connectionSlot = new Luau.AST.ElementAccess(store, luauFunction);
        var assign = new BinaryOperator(connectionSlot, "=", connect);

        // A bare 'event += fn;' statement doesn't need the connection's value, so the store
        // assignment can be emitted directly instead of stashed as a prereq of an unused expression.
        if (assignmentOperator.Parent is ExpressionStatement)
            return assign;

        _state.Prereq(new Luau.AST.ExpressionStatement(assign));
        return connectionSlot;
    }

    private LuauExpression GenerateEventDisconnect(AssignmentOperator assignmentOperator, EventTarget eventTarget)
    {
        var function = assignmentOperator.Right;
        if (function is Identifier identifier
            && _semanticModel.GetSymbol(identifier) is { } functionSymbol
            && _eventConnections.IsConnected(eventTarget, functionSymbol))
        {
            var store = GetConnectionStore(eventTarget);
            var connectionSlot = new Luau.AST.ElementAccess(store, Visit(function));
            return new Call(new Luau.AST.PropertyAccess(connectionSlot, ["Disconnect"]), [], true);
        }

        if (function is not Identifier && IsMethodReference(function))
        {
            _diagnostics.Error(
                function,
                InternalCodes.AnonymousEventDisconnect,
                "Cannot disconnect a function reference that gets wrapped into a new Luau closure on every connection.",
                "store the connection returned from '+=' and disconnect that instead."
            );

            return new NilLiteral();
        }

        _diagnostics.Error(
            assignmentOperator,
            InternalCodes.UnresolvedEventDisconnect,
            "No event connection exists for this function, connect it with '+=' before disconnecting it."
        );

        return new NilLiteral();
    }

    private Luau.AST.Identifier GetConnectionStore(EventTarget eventTarget) =>
        _eventConnections.GetOrCreateStore(eventTarget, () => _state.Scope.AddIdentifier(ConnectionStoreBaseName(eventTarget)));

    private static string ConnectionStoreBaseName(EventTarget eventTarget) =>
        eventTarget.Instance is Symbol instanceSymbol
            ? $"_{instanceSymbol.Name}_{eventTarget.Event.Name}_connections"
            : $"_{eventTarget.Event.Name}_connections";
}
