using Loom.Core.Parsing.AST;
using Loom.Core.TypeChecking.Types;
using Loom.Luau.AST;
using BinaryOperator = Loom.Luau.AST.BinaryOperator;
using ExpressionStatement = Loom.Luau.AST.ExpressionStatement;
using FunctionType = Loom.Core.TypeChecking.Types.FunctionType;
using Identifier = Loom.Luau.AST.Identifier;
using LiteralType = Loom.Core.TypeChecking.Types.LiteralType;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using PrimitiveTypeKind = Loom.Core.TypeChecking.Types.PrimitiveTypeKind;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.Generation;

public sealed partial class LuauGenerator
{
    // Set only while compiling a match arm's Guard expression, so a guard that references the arm's
    // own pattern-bound name (e.g. `n when n > 0`) resolves to the match subject instead of the not-yet-
    // declared local - the binding itself is only introduced inside the arm body, after the guard passes.
    private string? _guardSubstitutionName;
    private LuauExpression? _guardSubstitutionValue;

    public override LuauNode VisitMatchExpression(MatchExpression matchExpression)
    {
        if (matchExpression.Arms.Count == 0)
            return new NilLiteral();

        // A match with only a single, unguarded wildcard arm always takes that arm no matter what the
        // scrutinee is, so the subject/local-match scaffolding - and evaluating the scrutinee at all -
        // can be skipped entirely in favor of just the arm's body.
        if (matchExpression.Arms is [{ Pattern: WildcardPattern, Guard: null } soleArm])
            return Visit(soleArm.Body);

        var subject = _state.PushToVariable("_subject", Visit(matchExpression.Expression));
        var matchName = _state.Scope.AddIdentifier("_match");
        var matchIdentifier = new Identifier(matchName);
        _state.Prereq(new LocalVariable(matchName, null, null));

        LuauExpression? thenCondition = null;
        Chunk? thenBranch = null;
        var elseIfBranches = new List<ElseIfBranch>();
        Chunk? elseBranch = null;

        foreach (var arm in matchExpression.Arms)
        {
            if (elseBranch != null)
                break;

            var conditions = new List<LuauExpression>();
            var bindings = new List<LuauStatement>();
            if (!TryCompilePattern(arm.Pattern, subject, conditions, bindings, out var isIrrefutable))
                continue;

            if (arm.Guard != null)
            {
                conditions.Add(CompileGuard(arm, subject));
                isIrrefutable = false;
            }

            var armChunk = CompileMatchArmBody(arm, matchIdentifier, bindings);
            if (isIrrefutable)
            {
                elseBranch = armChunk;
                break;
            }

            var condition = CombineWith(conditions, "and");
            if (thenCondition == null)
            {
                thenCondition = condition;
                thenBranch = armChunk;
            }
            else
            {
                elseIfBranches.Add(new ElseIfBranch(condition, armChunk));
            }
        }

        if (thenCondition == null)
        {
            if (elseBranch != null)
                _state.Prereq([..elseBranch.Statements]);
        }
        else
        {
            _state.Prereq(new IfStatement(thenCondition, thenBranch!, elseIfBranches, elseBranch));
        }

        return matchIdentifier;
    }

    private LuauExpression CompileGuard(MatchArm arm, LuauExpression subject)
    {
        var substitutionName = arm.Pattern switch
        {
            IdentifierPattern identifierPattern => identifierPattern.Name.Text,
            LetPattern letPattern => letPattern.Name.Text,
            TypedPattern { ObjectPattern: null } typedPattern => typedPattern.Name.Text,
            _ => null
        };

        if (substitutionName == null)
            return (LuauExpression)Visit(arm.Guard!);

        var previousName = _guardSubstitutionName;
        var previousValue = _guardSubstitutionValue;
        _guardSubstitutionName = substitutionName;
        _guardSubstitutionValue = subject;
        try
        {
            return (LuauExpression)Visit(arm.Guard!);
        }
        finally
        {
            _guardSubstitutionName = previousName;
            _guardSubstitutionValue = previousValue;
        }
    }

    private Chunk CompileMatchArmBody(MatchArm arm, Identifier matchIdentifier, List<LuauStatement> bindings)
    {
        var statements = new List<LuauStatement>();
        var (body, scope) = _state.Capture(() =>
        {
            foreach (var binding in bindings)
                _state.Prereq(binding);

            return Visit(arm.Body);
        });

        ApplyPrereqAndPostreq(
            statements,
            scope,
            new ExpressionStatement(new BinaryOperator(matchIdentifier, "=", body))
        );

        return new Chunk(statements);
    }

    /// <summary>
    ///     Compiles a pattern against <paramref name="subject" />, appending any runtime checks the
    ///     pattern requires to <paramref name="conditions" /> (AND-ed together by the caller) and any
    ///     variable bindings the pattern introduces to <paramref name="bindings" /> (emitted inside the
    ///     arm body, after the conditions have already passed). Returns false - and reports a diagnostic
    ///     - for pattern kinds that still aren't supported, so the caller can skip the arm.
    /// </summary>
    private bool TryCompilePattern(Pattern pattern, LuauExpression subject, List<LuauExpression> conditions, List<LuauStatement> bindings, out bool isIrrefutable)
    {
        switch (pattern)
        {
            case WildcardPattern:
                isIrrefutable = true;
                return true;

            case IdentifierPattern identifierPattern:
                bindings.Add(new ConstVariable(identifierPattern.Name.Text, null, subject));
                isIrrefutable = true;
                return true;

            case LetPattern letPattern:
                bindings.Add(new ConstVariable(letPattern.Name.Text, null, subject));
                isIrrefutable = true;
                return true;

            case LiteralPattern literalPattern:
                conditions.Add(new BinaryOperator(subject, "==", LiteralValueToExpression(literalPattern.Value)));
                isIrrefutable = false;
                return true;

            case NullPattern:
                conditions.Add(new BinaryOperator(subject, "==", new NilLiteral()));
                isIrrefutable = false;
                return true;

            case RangePattern rangePattern:
                AddTypeofCondition(conditions, PrimitiveType.Number, subject);
                if (rangePattern.Minimum is LiteralPattern minimum)
                    conditions.Add(new BinaryOperator(subject, ">=", LiteralValueToExpression(minimum.Value)));
                if (rangePattern.Maximum is LiteralPattern maximum)
                    conditions.Add(new BinaryOperator(subject, "<=", LiteralValueToExpression(maximum.Value)));

                isIrrefutable = false;
                return true;

            case TypedPattern typedPattern:
            {
                AddTypeofCondition(conditions, _semanticModel.GetType(typedPattern.Type), subject);
                bindings.Add(new ConstVariable(typedPattern.Name.Text, null, subject));
                if (typedPattern.ObjectPattern != null && !CompileObjectPatternFields(typedPattern.ObjectPattern, subject, conditions, bindings))
                {
                    isIrrefutable = false;
                    return false;
                }

                isIrrefutable = false;
                return true;
            }

            case TypePattern typePattern:
            {
                AddTypeofCondition(conditions, _semanticModel.GetType(typePattern.Type), subject);
                if (typePattern.ObjectPattern != null && !CompileObjectPatternFields(typePattern.ObjectPattern, subject, conditions, bindings))
                {
                    isIrrefutable = false;
                    return false;
                }

                isIrrefutable = false;
                return true;
            }

            case ArrayPattern arrayPattern:
            {
                conditions.Add(new BinaryOperator(TypeofCall(subject), "==", new StringLiteral("table")));
                for (var i = 0; i < arrayPattern.Elements.Count; i++)
                {
                    var elementAccess = new Luau.AST.ElementAccess(subject, new NumberLiteral(i + 1));
                    if (!TryCompilePattern(arrayPattern.Elements[i], elementAccess, conditions, bindings, out _))
                    {
                        isIrrefutable = false;
                        return false;
                    }
                }

                if (arrayPattern.Rest != null)
                {
                    var rest = BuildArrayRestSlice(subject, arrayPattern.Elements.Count);
                    if (!TryCompilePattern(arrayPattern.Rest.Pattern, rest, conditions, bindings, out _))
                    {
                        isIrrefutable = false;
                        return false;
                    }
                }

                isIrrefutable = false;
                return true;
            }

            case ObjectPattern objectPattern:
                conditions.Add(new BinaryOperator(TypeofCall(subject), "==", new StringLiteral("table")));
                if (!CompileObjectPatternFields(objectPattern, subject, conditions, bindings))
                {
                    isIrrefutable = false;
                    return false;
                }

                isIrrefutable = false;
                return true;

            case OrPattern orPattern:
            {
                var alternativeConditions = new List<LuauExpression>();
                foreach (var alternative in orPattern.Patterns)
                {
                    var altConditions = new List<LuauExpression>();
                    var altBindings = new List<LuauStatement>();
                    if (!TryCompilePattern(alternative, subject, altConditions, altBindings, out var altIrrefutable))
                    {
                        isIrrefutable = false;
                        return false;
                    }

                    if (altIrrefutable)
                    {
                        isIrrefutable = true;
                        return true;
                    }

                    alternativeConditions.Add(CombineWith(altConditions, "and"));
                }

                conditions.Add(CombineWith(alternativeConditions, "or"));
                isIrrefutable = false;
                return true;
            }

            default:
                _diagnostics.NotImplemented(pattern, $"Pattern kind '{pattern.GetType().Name}' is not yet supported in code generation.");
                isIrrefutable = false;
                return false;
        }
    }

    private bool CompileObjectPatternFields(ObjectPattern objectPattern, LuauExpression subject, List<LuauExpression> conditions, List<LuauStatement> bindings)
    {
        foreach (var field in objectPattern.Fields)
        {
            var propertyAccess = new Luau.AST.PropertyAccess(subject, [field.Name.Text]);
            if (!TryCompilePattern(field.Pattern, propertyAccess, conditions, bindings, out _))
                return false;
        }

        return true;
    }

    // Rest-array codegen shape is a judgment call (upstream issue #82 leaves it undecided): slice the
    // remaining elements into a fresh table via `table.move(subject, N + 1, #subject, 1, {})`, which
    // copies subject[N+1..#subject] into a new table starting at index 1.
    private static LuauExpression BuildArrayRestSlice(LuauExpression subject, int elementCount)
    {
        var length = new Luau.AST.UnaryOperator("#", subject);
        return new Call(
            new Luau.AST.PropertyAccess(new Identifier("table"), ["move"]),
            [subject, new NumberLiteral(elementCount + 1), length, new NumberLiteral(1), new Table([])]
        );
    }

    private static void AddTypeofCondition(List<LuauExpression> conditions, Type type, LuauExpression subject)
    {
        var typeofString = GetLuauTypeofString(type);
        if (typeofString != null)
            conditions.Add(new BinaryOperator(TypeofCall(subject), "==", new StringLiteral(typeofString)));
    }

    private static Call TypeofCall(LuauExpression subject) => new(new Identifier("typeof"), [subject]);

    private static string? GetLuauTypeofString(Type type) =>
        type switch
        {
            PrimitiveType { Kind: PrimitiveTypeKind.Number } => "number",
            PrimitiveType { Kind: PrimitiveTypeKind.String } => "string",
            PrimitiveType { Kind: PrimitiveTypeKind.Bool } => "boolean",
            LiteralType { Value: long or int or double } => "number",
            LiteralType { Value: string } => "string",
            LiteralType { Value: bool } => "boolean",
            FunctionType => "function",
            InstantiatedType instantiated => GetLuauTypeofString(instantiated.Expand()),
            NativelyIndexableType => "table",
            _ => null
        };

    private static LuauExpression CombineWith(List<LuauExpression> conditions, string @operator) =>
        conditions.Count switch
        {
            0 => new BooleanLiteral(true),
            _ => conditions.Skip(1).Aggregate(conditions[0], (accumulated, next) => new BinaryOperator(accumulated, @operator, next))
        };

    private static LuauExpression LiteralValueToExpression(object? value) =>
        value switch
        {
            long l => new NumberLiteral(l),
            int i => new NumberLiteral(i),
            double d => new NumberLiteral(d),
            string s => new StringLiteral(s),
            bool b => new BooleanLiteral(b),
            _ => new NilLiteral()
        };
}
