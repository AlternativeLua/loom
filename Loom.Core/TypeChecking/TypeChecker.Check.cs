using Loom.Core.Diagnostics;
using Loom.Core.FlowAnalysis;
using Loom.Core.Parsing.AST;
using Loom.Core.Text;
using ArrayType = Loom.Core.TypeChecking.Types.ArrayType;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using Type = Loom.Core.TypeChecking.Types.Type;
using UnionType = Loom.Core.TypeChecking.Types.UnionType;

namespace Loom.Core.TypeChecking;

public sealed partial class TypeChecker
{
    private Type Check(Expression expression, Type expected) => Check(expression, expected, _flowState, out _);

    private Type Check(Expression expression, Type expected, FlowState state) => Check(expression, expected, state, out _);

    private Type Check(Expression expression, Type expected, FlowState state, out TypeSolver.TypeConstraint? constraint)
    {
        constraint = null;

        return expression switch
        {
            Parenthesized parenthesized => BindType(parenthesized, Check(parenthesized.Expression, expected, state, out constraint)),
            ArrayLiteral arrayLiteral when expected is ArrayType arrayType => CheckArrayLiteral(arrayLiteral, arrayType, state),
            TupleExpression tupleExpression when expected is Types.TupleType tupleType => CheckTupleExpression(tupleExpression, tupleType, state),
            MatchExpression matchExpression => CheckMatchExpression(matchExpression, expected, state),
            TernaryOperator ternaryOperator => CheckTernaryOperator(ternaryOperator, expected, state),
            BinaryOperator { Operator.Kind: SyntaxKind.QuestionQuestion or SyntaxKind.QuestionQuestionEquals } nullCoalesce => CheckNullCoalesce(
                nullCoalesce,
                expected,
                state
            ),
            InterfaceInvocation interfaceInvocation => CheckInterfaceInvocation(interfaceInvocation, expected, state),
            _ => CheckSubsumption(expression, expected, state, out constraint)
        };

    }
    
    private Type CheckSubsumption(Expression expression, Type expected, FlowState state, out TypeSolver.TypeConstraint? constraint)
    {
        constraint = null;
        var actual = Visit(expression, state);
        if (TryInstantiateGenericFunctionArgument(expression, actual, expected, out var instantiated))
            actual = instantiated;

        if (actual.IsAssignableTo(expected))
            return actual;

        constraint = _semanticModel.TypeSolver.AddConstraint(actual, expected, expression);
        return actual;
    }

    private ArrayType CheckArrayLiteral(ArrayLiteral arrayLiteral, ArrayType expected, FlowState state)
    {
        var elementTypes = new List<Type>(arrayLiteral.Expressions.Count);
        var elementConstraints = new List<TypeSolver.TypeConstraint>();
        foreach (var element in arrayLiteral.Expressions)
        {
            elementTypes.Add(Check(element, expected.ElementType, state, out var constraint));
            if (constraint != null)
                elementConstraints.Add(constraint);
        }

        if (elementConstraints.Count <= 0)
            return BindType(arrayLiteral, expected);

        var actualElementType = TypeSimplifier.Simplify(new UnionType(elementTypes.ConvertAll(t => t.Widen())));
        var actualArrayType = new ArrayType(actualElementType, expected.IsMutable);
        var trace = new TypeSolver.TypeMismatchTrace(actualArrayType, expected);
        foreach (var constraint in elementConstraints)
            constraint.Trace = trace;

        return BindType(arrayLiteral, expected);
    }

    private Types.TupleType CheckTupleExpression(TupleExpression tupleExpression, Types.TupleType expected, FlowState state)
    {
        if (tupleExpression.Expressions.Count != expected.ElementTypes.Count)
        {
            _diagnostics.Error(
                tupleExpression,
                InternalCodes.TupleArityMismatch,
                $"Tuple type '{expected}' expects {expected.ElementTypes.Count} element(s), but {tupleExpression.Expressions.Count} were provided."
            );

            return BindType(tupleExpression, expected);
        }

        for (var i = 0; i < tupleExpression.Expressions.Count; i++)
            Check(tupleExpression.Expressions[i], expected.ElementTypes[i], state);

        return BindType(tupleExpression, expected);
    }

    private Type CheckMatchExpression(MatchExpression matchExpression, Type expected, FlowState state)
    {
        var scrutineeType = Visit(matchExpression.Expression, state);
        if (matchExpression.Arms.Count == 0)
            return BindType(matchExpression, PrimitiveType.Never);

        var lastState = _flowState;
        _flowState = state;
        foreach (var arm in matchExpression.Arms)
            CheckMatchArm(arm, scrutineeType, expected);

        _flowState = lastState;

        return BindType(matchExpression, expected);
    }

    private Type CheckTernaryOperator(TernaryOperator ternaryOperator, Type expected, FlowState state)
    {
        var conditionType = Visit(ternaryOperator.Condition, state);
        _semanticModel.TypeSolver.AddConstraint(conditionType, PrimitiveType.Bool, ternaryOperator.Condition);

        var (trueState, falseState) = _narrower.ComputeBranchStates(ternaryOperator.Condition, state);
        Check(ternaryOperator.ThenBranch, expected, trueState);
        Check(ternaryOperator.ElseBranch, expected, falseState);

        return BindType(ternaryOperator, expected);
    }

    private Type CheckNullCoalesce(BinaryOperator nullCoalesce, Type expected, FlowState state)
    {
        var leftType = Visit(nullCoalesce.Left, state);
        var rightType = Check(nullCoalesce.Right, expected, state);

        if (!Type.IsOptional(leftType))
            _diagnostics.Warn(nullCoalesce, InternalCodes.RedundantCode, $"Null coalescing has no effect since '{leftType}' is not optional.");

        return BindType(nullCoalesce, TypeSimplifier.Simplify(new UnionType([leftType, rightType]).NonNullable()));
    }
}