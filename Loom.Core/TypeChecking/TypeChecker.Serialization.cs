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

        BuildImportedSchemas(builder);
    }

    /// <summary>
    ///     Schemas are per-file, so an interface imported from another module has none here and would
    ///     look unmarked. An import binding carries the exporting module's own symbol instance, so the
    ///     schema can simply be rebuilt - the generator then skips emitting it and references the
    ///     declaring module's codec instead.
    /// </summary>
    private void BuildImportedSchemas(SerializationSchemaBuilder builder)
    {
        foreach (var binding in _semanticModel.ImportBindings)
        {
            if (binding.Symbol is not InterfaceSymbol interfaceSymbol || _semanticModel.SerializationSchemas.ContainsKey(interfaceSymbol))
                continue;

            if (interfaceSymbol.Declaration is not InterfaceDeclaration declaration || !TryGetInterfaceAttribute(declaration, "serializable", out _))
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
        var unwrapped = Unwrap(propertyType);
        var isSizedNumber = unwrapped is Types.SizedNumberType;

        // A sized type already pins the width itself, so neither attribute has anything left to set -
        // this is what replaces 'number_type' for a plain number, and is the one place 'number_range'
        // is refused outright rather than just requiring a plain 'number'.
        if (isSizedNumber && hasNumberType)
        {
            _diagnostics.Error(
                property,
                InternalCodes.ConflictingAttributes,
                $"'{propertyName}' is already '{unwrapped}', so 'number_type' has nothing left to set.",
                $"remove 'number_type', or declare '{propertyName}: number' to pick a width with the attribute instead."
            );

            valid = false;
        }

        if (isSizedNumber && hasNumberRange)
        {
            _diagnostics.Error(
                property,
                InternalCodes.ConflictingAttributes,
                $"'{propertyName}' is already '{unwrapped}', so 'number_range' has nothing left to set.",
                $"remove 'number_range', or declare '{propertyName}: number' to use a bounded range instead."
            );

            valid = false;
        }

        // 'number_type' now has exactly one valid target left: an all-numeric tuple, where there is no
        // type-level way to set every element's width at once. Every other former target has either a
        // type-level replacement (a plain number's sized type, Vector3/Vector2/CFrame's own <T>) or, for
        // the other 7 Roblox datatypes, no replacement at all - their per-component width simply isn't
        // configurable anymore. Skipped when 'number_range' is also present: the conflict check below
        // already covers that combination with a more specific message.
        if (hasNumberType && !isSizedNumber && !hasNumberRange)
        {
            if (unwrapped is Types.PrimitiveType { Kind: Types.PrimitiveTypeKind.Number })
            {
                _diagnostics.Error(
                    property,
                    InternalCodes.InvalidAttributeTargetType,
                    $"'number_type' no longer applies to a plain 'number' - '{propertyName}' should be a sized type instead.",
                    $"use one of u8/u16/u32/i8/i16/i32/f32/f64, e.g. '{propertyName}: u8'."
                );

                valid = false;
            }
            else if (unwrapped is Types.InterfaceType { Name: "Vector3" or "Vector2" or "CFrame" } sizedComponentType)
            {
                _diagnostics.Error(
                    property,
                    InternalCodes.InvalidAttributeTargetType,
                    $"'number_type' no longer applies to '{propertyName}' - use '{sizedComponentType.Name}<i16>' instead of the attribute.",
                    $"declare it as '{propertyName}: {sizedComponentType.Name}<i16>', or drop the argument to keep the f32 default."
                );

                valid = false;
            }
            else if (unwrapped is Types.InterfaceType otherDatatype && RobloxDatatype.TryGet(otherDatatype.Name, out _))
            {
                _diagnostics.Error(
                    property,
                    InternalCodes.InvalidAttributeTargetType,
                    $"'number_type' on '{propertyName}' is no longer configurable - '{otherDatatype.Name}' always serializes its components as f32."
                );

                valid = false;
            }
            else if (!HasNumericComponents(propertyType))
            {
                _diagnostics.Error(
                    property,
                    InternalCodes.InvalidAttributeTargetType,
                    $"'number_type' requires '{propertyName}' to have numeric components, but it is '{propertyType}'."
                );

                valid = false;
            }
        }

        if (!isSizedNumber && hasNumberType && hasNumberRange)
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

        // 'number_type''s own non-numeric case is fully handled above; this only still needs to catch
        // 'number_range' (alone, or paired with 'number_type' - the conflict check above already flags
        // that combination, but a non-numeric target is a second, independent problem worth its own
        // message too).
        if (hasNumberRange && !isSizedNumber && !HasNumericComponents(propertyType))
        {
            _diagnostics.Error(
                property,
                InternalCodes.InvalidAttributeTargetType,
                $"'{(hasNumberType ? "number_type" : "number_range")}' requires '{propertyName}' to have numeric components, but it is '{propertyType}'."
            );

            valid = false;
        }

        if (present.Contains("length_type"))
        {
            _diagnostics.Error(
                property,
                InternalCodes.InvalidAttributeTargetType,
                $"'length_type' is no longer configurable via attribute on '{propertyName}'.",
                "use 'string<u8>' for a string's length width, or 'Array<T, u8>' for an array's."
            );

            valid = false;
        }

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

    /// <summary>
    ///     Whether <c>[number_type]</c> or <c>[number_range]</c> has anything to apply to. Vector3/Vector2/
    ///     CFrame and the other Roblox datatypes are deliberately absent - the former three source their
    ///     width from a type argument now, and the rest lost per-component configuration outright, so
    ///     neither attribute has a valid interface-shaped target left. Only an all-numeric tuple remains.
    /// </summary>
    private static bool HasNumericComponents(Type type) =>
        Unwrap(type) switch
        {
            Types.PrimitiveType { Kind: Types.PrimitiveTypeKind.Number } => true,
            // The attribute distributes to every element, so it applies as long as all of them are numeric.
            Types.TupleType tuple => tuple.ElementTypes.Count > 0 && tuple.ElementTypes.TrueForAll(HasNumericComponents),
            _ => false
        };

    private static bool IsNamedInterface(Type type, string name) => Unwrap(type) is Types.InterfaceType interfaceType && interfaceType.Name == name;

    /// <summary>
    ///     Looks through optionals, arrays, and generic instantiations, since an attribute on those applies
    ///     to the element - and Vector3/Vector2/CFrame are an <see cref="Types.InstantiatedType" /> now, not
    ///     a plain <see cref="Types.InterfaceType" />, so reaching their name at all requires expanding
    ///     first, same as every other reader of an instantiation elsewhere in the codebase already does.
    /// </summary>
    private static Type Unwrap(Type type) =>
        type switch
        {
            Types.OptionalType optional => Unwrap(optional.NonNullableType),
            Types.ArrayType array => Unwrap(array.ElementType),
            Types.InstantiatedType instantiated => Unwrap(instantiated.Expand()),
            _ => type
        };

    /// <summary>
    ///     Matches an intrinsic attribute by name. Symbol resolution is per-file, so an interface reached
    ///     through an import has attributes belonging to another file's tree that this model has no
    ///     reference entry for - the name is then all there is to go on.
    /// </summary>
    internal static bool IsIntrinsicAttributeNamed(Resolving.SemanticModel semanticModel, Attribute attribute, string name) =>
        semanticModel.GetSymbol(attribute.Expression) is { } symbol
            ? symbol is { IsIntrinsic: true } && symbol.Name == name
            : attribute.Expression is Identifier identifier && identifier.Name.Text == name;

    private bool TryGetInterfaceAttribute(InterfaceDeclaration interfaceDeclaration, string name, out Attribute attribute)
    {
        attribute = null!;
        if (interfaceDeclaration.Attributes == null)
            return false;

        var found = interfaceDeclaration.Attributes.AttributeList.Find(a => IsIntrinsicAttributeNamed(_semanticModel, a, name));

        if (found == null)
            return false;

        attribute = found;
        return true;
    }
}
