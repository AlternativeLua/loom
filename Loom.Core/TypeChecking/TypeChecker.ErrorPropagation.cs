using System.Diagnostics.CodeAnalysis;
using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.TypeChecking.Types;

namespace Loom.Core.TypeChecking;

using Type = Types.Type;

public sealed partial class TypeChecker
{
    public override Type VisitErrorPropagation(ErrorPropagation errorPropagation)
    {
        var operandType = Visit(errorPropagation.Expression);
        if (!TryGetResultTypeArguments(operandType, out var valueType, out var errorType))
        {
            _diagnostics.Error(
                errorPropagation,
                InternalCodes.ErrorPropagationRequiresResultType,
                $"The '?' operator can only be used on a value of type 'Result<T, E>', but got '{operandType}'."
            );

            return BindType(errorPropagation, Types.PrimitiveType.Never);
        }

        var enclosingReturnType = GetEnclosingDeclaredReturnType(errorPropagation);
        if (enclosingReturnType == null || !TryGetResultTypeArguments(enclosingReturnType, out _, out var enclosingErrorType))
        {
            _diagnostics.Error(
                errorPropagation,
                InternalCodes.ErrorPropagationOutsideResultFunction,
                "'?' can only be used inside of a function with a declared 'Result<T, E>' return type."
            );

            return BindType(errorPropagation, Types.PrimitiveType.Never);
        }

        if (!errorType.IsAssignableTo(enclosingErrorType))
        {
            _diagnostics.Error(
                errorPropagation,
                InternalCodes.ErrorPropagationErrorTypeMismatch,
                $"Cannot propagate error of type '{errorType}' through '?': the enclosing function's error type is '{enclosingErrorType}'."
            );

            return BindType(errorPropagation, Types.PrimitiveType.Never);
        }

        return BindType(errorPropagation, valueType);
    }

    private static bool TryGetResultTypeArguments(Type type, [MaybeNullWhen(false)] out Type value, [MaybeNullWhen(false)] out Type error)
    {
        if (type is InstantiatedType { GenericType.Declaration: TypeAlias { Name.Text: "Result" } } instantiated && instantiated.Arguments.Count == 2)
        {
            value = instantiated.Arguments[0];
            error = instantiated.Arguments[1];
            return true;
        }

        value = null;
        error = null;
        return false;
    }
}
