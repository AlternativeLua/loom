using System.Diagnostics.CodeAnalysis;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving;
using Loom.Luau;
using Loom.Luau.AST;
using BinaryOperator = Loom.Luau.AST.BinaryOperator;
using Identifier = Loom.Luau.AST.Identifier;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.Generation.Macros.Providers;

internal sealed class IntrinsicGlobalInvocationMacroProvider : IMacroProvider
{
    public bool Supports(SemanticModel _, Type __) => false;

    public bool Supports(SemanticModel semanticModel, Expression expression) =>
        expression is Parsing.AST.Identifier && semanticModel.GetSymbol(expression) is { IsIntrinsic: true };

    public bool IsInvocationOnlyMember(string memberName) => memberName is "string" or "number" or "new_instance" or "get_service" or "type_is";

    public bool TryInvocation(
        MacroContext context,
        string name,
        TypeArguments? typeArguments,
        Call call,
        [MaybeNullWhen(false)] out LuauExpression expression)
    {
        switch (name)
        {
            case "get_service":
                var serviceName = context.GetTextOfOnlyTypeArgument(typeArguments, name);
                expression = LuauFactory.LibraryCall("game", ["GetService"], [new StringLiteral(serviceName)], true);

                return true;
            case "new_instance":
                var instanceName = context.GetTextOfOnlyTypeArgument(typeArguments, name);
                expression = LuauFactory.LibraryCall("Instance", ["new"], [new StringLiteral(instanceName)]);

                return true;
            case "string":
                expression = TryFoldToString(call.Arguments[0], out var stringLiteral)
                    ? stringLiteral
                    : new Call(new Identifier("tostring"), call.Arguments);

                return true;
            case "number":
                expression = call.Arguments[0] is StringLiteral numberSource
                    ? LuauNumberFormat.TryParse(numberSource.Value, out var parsed) ? new NumberLiteral(parsed) : new NilLiteral()
                    : new Call(new Identifier("tonumber"), call.Arguments);

                return true;
            case "type_is":
                expression = new BinaryOperator(new Call(new Identifier("typeof"), [call.Arguments[0]]), "==", call.Arguments[1]);
                return true;
        }

        expression = null;
        return false;
    }

    private static bool TryFoldToString(LuauExpression argument, [MaybeNullWhen(false)] out StringLiteral folded)
    {
        switch (argument)
        {
            case StringLiteral stringLiteral:
                folded = stringLiteral;
                return true;
            case BooleanLiteral booleanLiteral:
                folded = new StringLiteral(booleanLiteral.Value ? "true" : "false");
                return true;
            case NilLiteral:
                folded = new StringLiteral("nil");
                return true;
            default:
                if (MacroContext.TryComputeConstantArithmetic(argument, out var number))
                {
                    folded = new StringLiteral(LuauNumberFormat.ToLuauString(number));
                    return true;
                }

                folded = null;
                return false;
        }
    }
}