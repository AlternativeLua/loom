using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Attribute = Loom.Core.Parsing.AST.Attribute;

namespace Loom.Core.TypeChecking;

using Type = Types.Type;

public sealed partial class TypeChecker
{
    private bool IsIntrinsicAttribute(Attribute attribute) => _semanticModel.GetSymbol(attribute.Expression)?.IsIntrinsic == true;

    private void CheckDecoratorAttribute(Attribute attribute, string valueName, Type valueType)
    {
        if (_resolvingHoisted.Count > 0)
            return;

        if (IsIntrinsicAttribute(attribute))
            return;

        if (_semanticModel.GetType(attribute) is not Types.FunctionType decoratorType)
            return;

        if (decoratorType.ParameterTypes.Count < 2)
        {
            _diagnostics.Error(
                attribute,
                InternalCodes.InvalidDecorator,
                "Decorators must accept the decorated value and its name as arguments."
            );

            return;
        }

        var thunkType = new Types.FunctionType([], [], valueType, false);
        var nameType = new Types.LiteralType(valueName);
        var argumentTypes = new List<Type> { thunkType, nameType };

        var substitution = decoratorType.TypeParameters.Count == 0
            ? null
            : TypeInferrer.InferFunctionTypeArguments(decoratorType, argumentTypes);

        var expectedParameterTypes = substitution == null
            ? decoratorType.ParameterTypes
            : SubstituteTypeParameters(attribute, decoratorType.ParameterTypes, substitution);

        if (!thunkType.IsAssignableTo(expectedParameterTypes[0]) || !nameType.IsAssignableTo(expectedParameterTypes[1]))
        {
            _diagnostics.Error(
                attribute,
                InternalCodes.InvalidDecorator,
                $"Decorator is not compatible with '{valueName}' of type '{valueType}'."
            );

            return;
        }

        var resultType = substitution == null ? decoratorType.ReturnType : SubstituteTypeParameters(attribute, decoratorType.ReturnType, substitution);
        if (!resultType.IsAssignableTo(valueType))
            _diagnostics.Error(
                attribute,
                InternalCodes.InvalidDecorator,
                $"Decorator must return a value assignable to '{valueType}', but returns '{resultType}'."
            );
    }
}
