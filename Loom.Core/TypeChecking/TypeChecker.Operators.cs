using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Text;
using Loom.Core.TypeChecking.Types;

namespace Loom.Core.TypeChecking;

using Type = Types.Type;

public sealed partial class TypeChecker
{
    public override Type VisitAssignmentOperator(AssignmentOperator assignmentOperator)
    {
        Type resultType;
        if (assignmentOperator.Operator.Kind != SyntaxKind.Equals)
        {
            resultType = VisitBinaryOperator(assignmentOperator);
        }
        else
        {
            // Resolved without any narrowing on the target still in effect - a prior `??=`/etc. narrows
            // what *reading* the variable sees, but checking a fresh value against it needs the full
            // declared type, or a legal `n = none` after `n` was narrowed to `number` would wrongly fail.
            var targetState = _narrower.WithoutNarrowing(assignmentOperator.Left, _flowState);
            var targetType = Visit(assignmentOperator.Left, targetState);
            var valueType = Check(assignmentOperator.Right, targetType);
            resultType = CheckImmutableAssignmentTarget(assignmentOperator, valueType);
        }

        NarrowAfterAssignment(assignmentOperator, resultType);
        return resultType;
    }

    // Only handled when the assignment is its own statement (the overwhelmingly common case, and the
    // one CheckStatements can actually thread forward via _exitStates) - an assignment nested inside a
    // larger expression doesn't get its own entry in the statement list to attach a narrowed exit state
    // to. Also skipped when resultType isn't even assignable to the target's own type, since that means
    // the "assignment" isn't really storing a new value into the target at all - e.g. `handler +=
    // callback` on an event field returns a ScriptConnection, not a new value for `handler`.
    private void NarrowAfterAssignment(AssignmentOperator assignmentOperator, Type resultType)
    {
        if (assignmentOperator.Parent is not ExpressionStatement statement)
            return;

        var targetType = _semanticModel.GetType(assignmentOperator.Left);
        if (!resultType.IsAssignableTo(targetType))
            return;

        if (_narrower.TryNarrowAfterAssignment(assignmentOperator.Left, resultType, _flowState) is { } narrowed)
            _exitStates[statement] = narrowed;
    }

    private Type CheckImmutableAssignmentTarget(AssignmentOperator assignmentOperator, Type valueType)
    {
        if (assignmentOperator.Left is not (ElementAccess or PropertyAccess or QualifiedName))
            return BindType(assignmentOperator, valueType);

        var expression = assignmentOperator.Left switch
        {
            ElementAccess access => access.Expression,
            PropertyAccess propertyAccess => propertyAccess.Expression,
            QualifiedName name => name.Identifier,
            _ => null!
        };

        var expressionType = _semanticModel.GetType(expression);
        if (expressionType is Types.PrimitiveType { Kind: Types.PrimitiveTypeKind.String })
            expressionType = Intrinsics.StringMembers;

        var indexType = assignmentOperator.Left switch
        {
            ElementAccess access => _semanticModel.GetType(access.IndexExpression),
            PropertyAccess propertyAccess => new Types.LiteralType(propertyAccess.Names.First().Name.Text),
            QualifiedName name => new Types.LiteralType(name.Names.First().Name.Text),
            _ => null!
        };

        if (expressionType is not NativelyIndexableType indexableType)
            return BindType(assignmentOperator, valueType);

        var names = (assignmentOperator.Left switch
        {
            PropertyAccess propertyAccess => propertyAccess.Names,
            QualifiedName name => name.Names,
            _ => []
        }).ToList();

        if (names.Count > 1)
        {
            foreach (var name in names.SkipLast(1))
            {
                var property = indexableType.GetProperty(name.Name.Text);
                if (property?.ValueType is not NativelyIndexableType nestedIndexable)
                    return BindType(assignmentOperator, valueType);

                indexableType = nestedIndexable;
            }

            indexType = new Types.LiteralType(names.Last().Name.Text);
        }

        var (bodyType, _) = indexableType.GetTypeAtIndex(indexType);
        if (bodyType is not { IsMutable: false })
            return BindType(assignmentOperator, valueType);

        var display = bodyType switch
        {
            ObjectProperty property => $"property '{property.Name}'.",
            ObjectIndexer indexer => $"index '{indexer.KeyType}'.",
            _ => ""
        };

        _diagnostics.Error(assignmentOperator, InternalCodes.AssignToImmutable, $"Cannot assign to immutable {display}");

        // Dropping AddConstraint here because the Check method already does it
        return BindType(assignmentOperator, valueType);
    }

    public override Type VisitTernaryOperator(TernaryOperator ternaryOperator)
    {
        var conditionType = Visit(ternaryOperator.Condition);
        _semanticModel.TypeSolver.AddConstraint(conditionType, Types.PrimitiveType.Bool, ternaryOperator.Condition);

        var (trueState, falseState) = _narrower.ComputeBranchStates(ternaryOperator.Condition, _flowState);
        var thenBranchType = Visit(ternaryOperator.ThenBranch, trueState);
        var elseBranchType = Visit(ternaryOperator.ElseBranch, falseState);
        var union = new Types.UnionType([thenBranchType, elseBranchType]);
        return BindType(ternaryOperator, TypeSimplifier.Simplify(union));
    }

    public override Type VisitBinaryOperator(BinaryOperator binaryOperator)
    {
        var leftType = Visit(binaryOperator.Left);
        Type rightType;
        switch (binaryOperator.Operator.Kind)
        {
            case SyntaxKind.AmpersandAmpersand or SyntaxKind.AmpersandAmpersandEquals:
                var (trueState, _) = _narrower.ComputeBranchStates(binaryOperator.Left, _flowState);
                rightType = Visit(binaryOperator.Right, trueState);
                break;
            case SyntaxKind.PipePipe or SyntaxKind.PipePipeEquals:
                var (_, falseState) = _narrower.ComputeBranchStates(binaryOperator.Left, _flowState);
                rightType = Visit(binaryOperator.Right, falseState);
                break;
            default:
                rightType = Visit(binaryOperator.Right);
                break;
        }

        var rule = BinaryOperatorBinder.GetRule(binaryOperator, leftType, rightType);
        if (rule != null)
        {
            _semanticModel.TypeSolver.AddConstraint(leftType, rule.LeftType, binaryOperator.Left);
            _semanticModel.TypeSolver.AddConstraint(rightType, rule.RightType, binaryOperator.Right);
            return BindType(binaryOperator, rule.ReturnType);
        }

        switch (binaryOperator.Operator.Kind)
        {
            case SyntaxKind.QuestionQuestion or SyntaxKind.QuestionQuestionEquals:
            {
                if (!Type.IsOptional(leftType))
                    _diagnostics.Warn(
                        binaryOperator,
                        InternalCodes.RedundantCode,
                        $"Null coalescing has no effect since '{leftType}' is not optional."
                    );

                return BindType(binaryOperator, TypeSimplifier.Simplify(new Types.UnionType([leftType, rightType]).NonNullable()));
            }
            case SyntaxKind.PlusEquals or SyntaxKind.MinusEquals
                when TryGetEventParameterTypes(binaryOperator, leftType, out var eventParameters):
            {
                var assignableFunction = new Types.FunctionType([], eventParameters, Types.PrimitiveType.Void);
                _semanticModel.TypeSolver.AddConstraint(rightType, assignableFunction, binaryOperator.Right);
                return BindType(binaryOperator, GetIntrinsicType(binaryOperator, "ScriptConnection"));
            }
        }

        var suggestion = BinaryOperatorBinder.GetSuggestion(binaryOperator, leftType, rightType);
        var hint = Diagnostic.FormatBinaryHint(binaryOperator, leftType, rightType, suggestion);
        _diagnostics.Error(
            binaryOperator,
            InternalCodes.InvalidBinaryOp,
            $"No binary operation for '{leftType.Widen()}' {binaryOperator.Operator.Text} '{rightType.Widen()}'.",
            hint
        );

        return BindType(binaryOperator, Types.PrimitiveType.Never);
    }

    public override Type VisitUnaryOperator(UnaryOperator unaryOperator)
    {
        var operandType = Visit(unaryOperator.Operand);
        var rule = UnaryOperatorBinder.GetRule(unaryOperator, operandType);
        if (rule != null)
            return rule.ReturnType;

        var suggestion = UnaryOperatorBinder.GetSuggestion(unaryOperator, operandType);
        var hint = Diagnostic.FormatUnaryHint(unaryOperator, operandType, suggestion);
        _diagnostics.Error(unaryOperator, InternalCodes.InvalidUnaryOp, $"No unary operation for {unaryOperator.Operator.Text}{operandType.Widen()}.", hint);

        return BindType(unaryOperator, Types.PrimitiveType.Never);
    }
}
