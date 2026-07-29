using System.Diagnostics.CodeAnalysis;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving;
using Loom.Luau;
using Loom.Luau.AST;
using BinaryOperator = Loom.Luau.AST.BinaryOperator;
using Parenthesized = Loom.Luau.AST.Parenthesized;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using Type = Loom.Core.TypeChecking.Types.Type;
using UnaryOperator = Loom.Luau.AST.UnaryOperator;

namespace Loom.Core.Generation.Macros.Providers;

internal sealed class StringMacroProvider : IMacroProvider
{
    public bool Supports(SemanticModel _, Type type) => type.IsAssignableTo(PrimitiveType.String);
    public bool Supports(SemanticModel _, Expression __) => false;

    public bool IsInvocationOnlyMember(string memberName) =>
        memberName is "upper" or "lower" or "trim" or "replace" or "reverse" or "repeat" or "split" or "has" or "starts_with" or "ends_with" or "byte";

    public bool TryProperty(MacroContext context, string name, LuauExpression target, [MaybeNullWhen(false)] out LuauExpression expression)
    {
        switch (name)
        {
            case "length":
            {
                expression = new UnaryOperator("#", target);
                return true;
            }
        }

        expression = null;
        return false;
    }

    public bool TryInvocation(
        MacroContext context,
        string name,
        TypeArguments? typeArguments,
        Call call,
        [MaybeNullWhen(false)] out LuauExpression expression)
    {
        var str = MacroContext.GetCallObject(call);
        switch (name)
        {
            case "upper":
            {
                expression = LuauFactory.StringCall("upper", [str]);
                return true;
            }
            case "lower":
            {
                expression = LuauFactory.StringCall("lower", [str]);
                return true;
            }
            case "reverse":
            {
                expression = LuauFactory.StringCall("reverse", [str]);
                return true;
            }
            case "split":
            {
                expression = LuauFactory.StringCall("split", [str, ..call.Arguments]);
                return true;
            }
            case "repeat":
            {
                expression = LuauFactory.StringCall("rep", [str, ..call.Arguments]);
                return true;
            }
            case "byte":
            {
                expression = LuauFactory.StringCall("byte", [str, ..call.Arguments]);
                return true;
            }
            case "trim":
            {
                expression = new Parenthesized(
                    LuauFactory.StringCall("gsub", [str, new StringLiteral("^%s*(.-)%s*$"), new StringLiteral("%1")])
                );

                return true;
            }
            case "replace":
            {
                expression = new Parenthesized(LuauFactory.StringCall("gsub", [str, ..call.Arguments]));
                return true;
            }
            case "has":
            {
                expression = new BinaryOperator(
                    LuauFactory.StringCall("find", [str, call.Arguments.Single(), new NumberLiteral(1), new BooleanLiteral(true)]),
                    "~=",
                    new NilLiteral()
                );

                return true;
            }
            case "starts_with":
            {
                var prefix = context.State.PushToVariable("_prefix", call.Arguments.Single());
                expression = new BinaryOperator(
                    LuauFactory.StringCall("sub", [str, new NumberLiteral(1), new UnaryOperator("#", prefix)]),
                    "==",
                    prefix
                );

                return true;
            }
            case "ends_with":
            {
                var self = context.State.PushToVariable("_str", str);
                var suffix = context.State.PushToVariable("_suffix", call.Arguments.Single());
                var start = new BinaryOperator(
                    new BinaryOperator(new UnaryOperator("#", self), "-", new UnaryOperator("#", suffix)),
                    "+",
                    new NumberLiteral(1)
                );

                expression = new BinaryOperator(LuauFactory.StringCall("sub", [self, start]), "==", suffix);
                return true;
            }
        }

        expression = null;
        return false;
    }
}
