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

        if (expression is Parenthesized parenthesized)
            return BindType(parenthesized, Check(parenthesized.Expression, expected, state, out constraint));

        if (expression is ArrayLiteral arrayLiteral && expected is ArrayType arrayType)
            return CheckArrayLiteral(arrayLiteral, arrayType, state);

        if (expression is TupleExpression tupleExpression && expected is Types.TupleType tupleType)
            return CheckTupleExpression(tupleExpression, tupleType, state);

        if (expression is MatchExpression matchExpression)
            return CheckMatchExpression(matchExpression, expected, state);

        if (expression is TernaryOperator ternaryOperator)
            return CheckTernaryOperator(ternaryOperator, expected, state);

        if (expression is BinaryOperator { Operator.Kind: SyntaxKind.QuestionQuestion or SyntaxKind.QuestionQuestionEquals } nullCoalesce)
            return CheckNullCoalesce(nullCoalesce, expected, state);

        if (expression is InterfaceInvocation interfaceInvocation)
            return CheckInterfaceInvocation(interfaceInvocation, expected, state);

        // Once more specific rules, add more, but for now it'll just be like that.
        return CheckSubsumption(expression, expected, state, out constraint);
    }

    private Type CheckSubsumption(Expression expression, Type expected, FlowState state) =>
        CheckSubsumption(expression, expected, state, out _);

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

        if (elementConstraints.Count > 0)
        {
            var actualElementType = TypeSimplifier.Simplify(new UnionType(elementTypes.ConvertAll(t => t.Widen())));
            var actualArrayType = new ArrayType(actualElementType, expected.IsMutable);
            var trace = new TypeSolver.TypeMismatchTrace(actualArrayType, expected);
            foreach (var constraint in elementConstraints)
                constraint.Trace = trace;
        }

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