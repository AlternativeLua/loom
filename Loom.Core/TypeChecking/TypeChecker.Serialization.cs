using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving.Symbols;
using Loom.Core.TypeChecking.Serialization;
using Attribute = Loom.Core.Parsing.AST.Attribute;

namespace Loom.Core.TypeChecking;

using Type = Types.Type;

public sealed partial class TypeChecker
{
    /// <summary>Property-level serialization attributes, in the order they are reported.</summary>
    private static readonly string[] _serializationPropertyAttributes =
        ["number_type", "number_range", "number_step", "length_type", "cframe_type"];

    /// <summary>
    ///     Interfaces are checked after <c>SolveConstraints</c> rather than inline, so a serializable type
    ///     may reference one declared further down the file without its properties being half-resolved.
    /// </summary>
    private readonly List<InterfaceDeclaration> _interfaceDeclarations = [];

    private void CheckSerializableInterfaces()
    {
        var builder = new SerializationSchemaBuilder(_semanticModel, _diagnostics);
        foreach (var interfaceDeclaration in _interfaceDeclarations)
        {
            if (_semanticModel.GetDeclarationSymbol(interfaceDeclaration, SymbolKind.Interface) is not InterfaceSymbol interfaceSymbol)
                continue;

            var isSerializable = TryGetInterfaceAttribute(interfaceDeclaration, "serializable", out _);
            if (!CheckSerializationAttributes(interfaceDeclaration, isSerializable))
                continue;

            if (!isSerializable)
                continue;

            if (builder.Build(interfaceSymbol) is { } schema)
                _semanticModel.SerializationSchemas[interfaceSymbol] = schema;
        }
    }

    /// <summary>
    ///     Enforces the attribute matrix: which attributes require which others, which cannot appear
    ///     together, and which property types each one accepts. Returns false when the interface is too
    ///     malformed to build a schema from.
    /// </summary>
    private bool CheckSerializationAttributes(InterfaceDeclaration interfaceDeclaration, bool isSerializable)
    {
        var name = interfaceDeclaration.Name.Text;
        var valid = true;

        if (TryGetInterfaceAttribute(interfaceDeclaration, "packed", out var packedAttribute) && !isSerializable)
        {
            _diagnostics.Error(
                packedAttribute,
                InternalCodes.MissingRequiredAttribute,
                $"'packed' requires interface '{name}' to also have the 'serializable' attribute.",
                "'packed' only changes how a serializable type is encoded."
            );

            valid = false;
        }

        foreach (var property in interfaceDeclaration.Body?.Members.OfType<PropertyDeclaration>() ?? [])
            valid &= CheckPropertySerializationAttributes(property, name, isSerializable);

        return valid;
    }

    private bool CheckPropertySerializationAttributes(PropertyDeclaration property, string interfaceName, bool isSerializable)
    {
        var present = _serializationPropertyAttributes
            .Where(a => property.TryGetIntrinsicAttribute(_semanticModel, a, out _))
            .ToList();

        var isIgnored = property.TryGetIntrinsicAttribute(_semanticModel, "ignore_serialization", out var ignoreAttribute);
        if (present.Count == 0 && !isIgnored)
            return true;

        var propertyName = property.Name.Text;
        if (!isSerializable)
        {
            var reported = isIgnored ? "ignore_serialization" : present[0];
            _diagnostics.Error(
                property,
                InternalCodes.MissingRequiredAttribute,
                $"'{reported}' requires interface '{interfaceName}' to have the 'serializable' attribute.",
                $"add 'serializable' to '{interfaceName}', or remove the attribute from '{propertyName}'."
            );

            return false;
        }

        var valid = true;
        var propertyType = _semanticModel.GetType(property.ColonTypeClause.Type);

        if (isIgnored)
        {
            if (present.Count > 0)
            {
                _diagnostics.Error(
                    property,
                    InternalCodes.ConflictingAttributes,
                    $"'{propertyName}' is both ignored and annotated with '{present[0]}'.",
                    "an ignored property is not encoded, so it cannot carry encoding attributes."
                );

                valid = false;
            }

            // Deserialization has to produce a value satisfying the declared type, and Loom interfaces
            // have no property defaults to restore from - so the only sound target is an optional.
            if (propertyType is not Types.OptionalType)
            {
                _diagnostics.Error(
                    ignoreAttribute!.Attribute,
                    InternalCodes.InvalidAttributeTargetType,
                    $"'ignore_serialization' requires '{propertyName}' to be optional, since there is no default value to restore.",
                    $"declare it as '{propertyName}: {propertyType}?'."
                );

                valid = false;
            }

            return valid;
        }

        var hasNumberType = present.Contains("number_type");
        var hasNumberRange = present.Contains("number_range");
        if (hasNumberType && hasNumberRange)
        {
            _diagnostics.Error(
                property,
                InternalCodes.ConflictingAttributes,
                $"'{propertyName}' has both 'number_type' and 'number_range', which each set its width.",
                "use 'number_range' for a bounded value, or 'number_type' for a fixed width."
            );

            valid = false;
        }

        if (present.Contains("number_step") && !hasNumberRange)
        {
            _diagnostics.Error(
                property,
                InternalCodes.MissingRequiredAttribute,
                $"'number_step' on '{propertyName}' requires 'number_range'.",
                "without bounds there is no bit width to derive from a step."
            );

            valid = false;
        }

        if ((hasNumberType || hasNumberRange) && !HasNumericComponents(propertyType))
        {
            _diagnostics.Error(
                property,
                InternalCodes.InvalidAttributeTargetType,
                $"'{(hasNumberType ? "number_type" : "number_range")}' requires '{propertyName}' to have numeric components, but it is '{propertyType}'."
            );

            valid = false;
        }

        if (present.Contains("length_type"))
            valid &= CheckLengthTypeAttribute(property, propertyName, propertyType);

        if (present.Contains("cframe_type") && !IsNamedInterface(propertyType, "CFrame"))
        {
            _diagnostics.Error(
                property,
                InternalCodes.InvalidAttributeTargetType,
                $"'cframe_type' requires '{propertyName}' to be a CFrame, but it is '{propertyType}'."
            );

            valid = false;
        }

        return valid;
    }

    private bool CheckLengthTypeAttribute(PropertyDeclaration property, string propertyName, Type propertyType)
    {
        var valid = true;
        if (!IsLengthPrefixed(propertyType))
        {
            _diagnostics.Error(
                property,
                InternalCodes.InvalidAttributeTargetType,
                $"'length_type' requires '{propertyName}' to be a string or array, but it is '{propertyType}'."
            );

            valid = false;
        }

        if (!property.TryGetIntrinsicAttribute(_semanticModel, "length_type", out var attribute)
            || attribute.Attribute.Arguments.ArgumentList is not [var argument]
            || _semanticModel.GetConstantValue(argument) is not double ordinal)
            return valid;

        var numberType = (NumberType)(int)ordinal;
        if (numberType.IsUnsigned())
            return valid;

        _diagnostics.Error(
            argument,
            InternalCodes.InvalidAttributeTargetType,
            $"'length_type' on '{propertyName}' must be an unsigned number type, but is '{numberType}'.",
            "lengths are never negative; use U8, U16, or U32."
        );

        return false;
    }

    /// <summary>Whether <c>[number_type]</c> or <c>[number_range]</c> has anything to apply to.</summary>
    private static bool HasNumericComponents(Type type) =>
        Unwrap(type) switch
        {
            Types.PrimitiveType { Kind: Types.PrimitiveTypeKind.Number } => true,
            Types.InterfaceType interfaceType => interfaceType.Name == "CFrame" || RobloxDatatype.TryGet(interfaceType.Name, out _),
            // The attribute distributes to every element, so it applies as long as all of them are numeric.
            Types.TupleType tuple => tuple.ElementTypes.Count > 0 && tuple.ElementTypes.TrueForAll(HasNumericComponents),
            _ => false
        };

    private static bool IsLengthPrefixed(Type type) =>
        Unwrap(type) is Types.ArrayType or Types.PrimitiveType { Kind: Types.PrimitiveTypeKind.String };

    private static bool IsNamedInterface(Type type, string name) => Unwrap(type) is Types.InterfaceType interfaceType && interfaceType.Name == name;

    /// <summary>Looks through optionals and arrays, since an attribute on those applies to the element.</summary>
    private static Type Unwrap(Type type) =>
        type switch
        {
            Types.OptionalType optional => Unwrap(optional.NonNullableType),
            Types.ArrayType array => Unwrap(array.ElementType),
            _ => type
        };

    private bool TryGetInterfaceAttribute(InterfaceDeclaration interfaceDeclaration, string name, out Attribute attribute)
    {
        attribute = null!;
        if (interfaceDeclaration.Attributes == null)
            return false;

        var found = interfaceDeclaration.Attributes.AttributeList
            .Find(a => _semanticModel.GetSymbol(a.Expression) is { IsIntrinsic: true } symbol && symbol.Name == name);

        if (found == null)
            return false;

        attribute = found;
        return true;
    }
}
