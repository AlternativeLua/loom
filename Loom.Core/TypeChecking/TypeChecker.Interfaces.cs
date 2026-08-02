using System.Diagnostics.CodeAnalysis;
using Loom.Core.Diagnostics;
using Loom.Core.FlowAnalysis;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving.Symbols;
using Loom.Core.TypeChecking.Types;
using FunctionType = Loom.Core.TypeChecking.Types.FunctionType;
using IntersectionType = Loom.Core.TypeChecking.Types.IntersectionType;
using LiteralType = Loom.Core.TypeChecking.Types.LiteralType;
using PrimitiveType = Loom.Core.TypeChecking.Types.PrimitiveType;
using Type = Loom.Core.TypeChecking.Types.Type;
using TypePredicateType = Loom.Core.Parsing.AST.TypePredicateType;

namespace Loom.Core.TypeChecking;

public sealed partial class TypeChecker
{
    public override Type VisitImplement(Implement implement)
    {
        var traitType = Visit(implement.TraitName);
        var interfaceType = Visit(implement.InterfaceName);
        foreach (var declaration in implement.Body.Implementations)
        {
            var declarationType = (FunctionType)GetTypeAtIndex(declaration, traitType, new LiteralType(declaration.Name.Text));
            BindType(declaration, declarationType);
            MaybeVisit(declaration.TypeParameters);

            for (var i = 0; i < declarationType.ParameterTypes.Count; i++)
            {
                var parameter = declaration.Parameters!.ParameterList[i];
                var explicitType = MaybeVisit(parameter.ColonTypeClause);
                var initializerType = MaybeVisit(parameter.EqualsValueClause);
                var type = declarationType.ParameterTypes[i];
                if (parameter.EqualsValueClause != null)
                    _semanticModel.TypeSolver.AddConstraint(initializerType!, type, parameter.EqualsValueClause.Value);

                if (parameter.EqualsValueClause != null && Type.IsOptional(type))
                    type = type.NonNullable();

                if (explicitType != null)
                    _semanticModel.TypeSolver.AddConstraint(explicitType, type, parameter.ColonTypeClause!.Type);

                BindType(parameter, type);
            }

            var actualType = GetReturnType(declaration);
            _semanticModel.TypeSolver.AddConstraint(actualType, declarationType.ReturnType, declaration.ReturnType?.Type.LocationSpan ?? declaration.LocationSpan);
            if (declaration.ReturnType != null)
                BindType(declaration.ReturnType, declarationType.ReturnType);

            Visit(declaration.Body);
        }

        return TypeSimplifier.Simplify(new IntersectionType([traitType, interfaceType]));
    }

    public override Type VisitSelfExpression(SelfExpression selfExpression)
    {
        var implement = selfExpression.FirstAncestorOfType<Implement>();
        if (implement == null)
        {
            if (selfExpression.Parent is TypePredicateType
                && (selfExpression.FirstAncestorOfType<InterfaceDeclaration>() != null || selfExpression.FirstAncestorOfType<TraitDeclaration>() != null))
                return BindType(selfExpression, PrimitiveType.Unknown);

            return BindType(selfExpression, PrimitiveType.Never);
        }

        var interfaceType = _semanticModel.GetType(implement.InterfaceName);
        if (interfaceType is not InterfaceType nonGenericInterfaceType
            || _semanticModel.GetSymbol(implement.InterfaceName, SymbolKind.Interface) is not InterfaceSymbol interfaceSymbol)
            return BindType(selfExpression, interfaceType);

        var traitProperties = interfaceSymbol.FullImplementations
            .SelectMany(i => i.Body.Implementations)
            .Select(declaration => new ObjectProperty(false, declaration.Name.Text, _semanticModel.GetType(declaration)))
            .ToList();

        var objectType = new ObjectType(nonGenericInterfaceType.ObjectType.Indexer, [..nonGenericInterfaceType.ObjectType.Properties, ..traitProperties]);
        var selfType = new InterfaceType(nonGenericInterfaceType.Name, nonGenericInterfaceType.Constraints, objectType)
        {
            TraitMethodNames = traitProperties.ConvertAll(property => property.Name).ToHashSet()
        };

        return BindType(selfExpression, selfType);
    }

    public override Type VisitTraitDeclaration(TraitDeclaration traitDeclaration)
    {
        var name = traitDeclaration.Name.Text;
        var typeParameters = traitDeclaration.TypeParameters?.ParameterList.ConvertAll(VisitTypeParameter);
        var objectType = new ObjectType(null, []);
        var interfaceType = new InterfaceType(name, [], objectType);
        Type publishedType = typeParameters == null
            ? interfaceType
            : new GenericType(traitDeclaration, typeParameters, interfaceType);

        BindType(traitDeclaration, publishedType);

        var properties = ResolveTraitProperties(traitDeclaration.Body.Members);
        objectType.AddProperties(properties);

        if (publishedType is GenericType generic)
            publishedType = VarianceInferrer.ApplyInferredVariance(generic);

        return BindType(traitDeclaration, publishedType);
    }

    public override Type VisitInterfaceDeclaration(InterfaceDeclaration interfaceDeclaration)
    {
        MaybeVisit(interfaceDeclaration.Attributes);

        var name = interfaceDeclaration.Name.Text;
        if (_semanticModel.GetDeclarationSymbol(interfaceDeclaration, SymbolKind.Interface) is not InterfaceSymbol)
        {
            _diagnostics.Error(interfaceDeclaration, InternalCodes.CannotFindSymbol, $"Cannot find symbol for declaration of interface '{name}'.");
            return BindType(interfaceDeclaration, PrimitiveType.Never);
        }

        var typeParameters = interfaceDeclaration.TypeParameters?.ParameterList.ConvertAll(VisitTypeParameter);
        var constraints = interfaceDeclaration.ColonTypeListClause?.Types
                .Select(Visit)
                .Select(TypeSimplifier.Simplify)
                .OfType<InterfaceType>()
                .ToList()
            ?? [];

        var objectType = new ObjectType(null, []);
        var interfaceType = new InterfaceType(name, constraints, objectType);
        Type publishedType = typeParameters == null
            ? interfaceType
            : new GenericType(interfaceDeclaration, typeParameters, interfaceType);

        BindType(interfaceDeclaration, publishedType);

        var indexerDeclaration = interfaceDeclaration.Body?.Members.OfType<IndexerDeclaration>().FirstOrDefault();
        var indexer = ResolveInterfaceIndexer(constraints, indexerDeclaration);
        objectType.Indexer = indexer;

        var eventDeclarations = interfaceDeclaration.Body?.Members.OfType<EventDeclaration>().ToList() ?? [];
        var propertyDeclarations = interfaceDeclaration.Body?.Members.OfType<PropertyDeclaration>().ToList() ?? [];
        var events = ResolveInterfaceEvents(eventDeclarations);
        var properties = ResolveInterfaceProperties(constraints, propertyDeclarations);
        objectType.AddProperties(events);
        objectType.AddProperties(properties);

        if (interfaceDeclaration.Attributes != null)
            foreach (var attribute in interfaceDeclaration.Attributes.AttributeList)
            {
                CheckPassiveDecorator(attribute);
                CheckAttributeUsage(attribute, AttributeTargetsFlag.Interface);
            }

        if (publishedType is GenericType generic)
            publishedType = VarianceInferrer.ApplyInferredVariance(generic);

        return BindType(interfaceDeclaration, publishedType);
    }

    public override Type VisitInterfaceInvocation(InterfaceInvocation interfaceInvocation) =>
        CheckOrVisitInterfaceInvocation(interfaceInvocation, null);

    private Type CheckInterfaceInvocation(InterfaceInvocation interfaceInvocation, Type expected, FlowState state)
    {
        var lastState = _flowState;
        _flowState = state;
        var result = CheckOrVisitInterfaceInvocation(interfaceInvocation, expected);
        _flowState = lastState;

        return result;
    }

    private Type CheckOrVisitInterfaceInvocation(InterfaceInvocation interfaceInvocation, Type? expected)
    {
        var type = Visit(interfaceInvocation.Name);
        if (type.Equals(Intrinsics.Range))
            _diagnostics.Warn(interfaceInvocation, InternalCodes.SimplifiableCode, "Use a range literal.");

        var traitProperties = new List<ObjectProperty>();
        if (_semanticModel.GetSymbol(interfaceInvocation.Name, SymbolKind.Interface) is InterfaceSymbol interfaceSymbol)
            traitProperties.AddRange(
                from declaration in interfaceSymbol.Implementations.SelectMany(i => i.Body.Implementations)
                let methodType = _semanticModel.GetType(declaration)
                select new ObjectProperty(false, declaration.Name.Text, methodType)
            );

        if (type is InterfaceType nonGeneric)
            return BindInterfaceInvocation(interfaceInvocation, nonGeneric, traitProperties);

        if (type is not GenericType { UnderlyingType: InterfaceType underlying } generic)
        {
            _diagnostics.Error(interfaceInvocation, InternalCodes.InvalidInvocation, $"Type '{type}' is not an interface.");
            return BindType(interfaceInvocation, PrimitiveType.Never);
        }

        if (!TrySubstituteGenericInterface(interfaceInvocation, generic, underlying, expected, out var interfaceType))
            return BindType(interfaceInvocation, PrimitiveType.Never);

        return BindInterfaceInvocation(interfaceInvocation, interfaceType, traitProperties);
    }

    private InterfaceType BindInterfaceInvocation(InterfaceInvocation node, InterfaceType interfaceType, List<ObjectProperty> traitProperties)
    {
        CheckInterfaceInvocationInitializers(node, interfaceType);

        // A fresh ObjectType/InterfaceType is built here rather than mutating interfaceType.ObjectType in place,
        // since interfaceType is the shared instance cached for the interface declaration; mutating it would leak
        // trait methods into the structural property list for every other construction site of the same interface.
        var traitMethodNames = traitProperties.Select(p => p.Name).ToHashSet();
        var objectType = new ObjectType(interfaceType.ObjectType.Indexer, [..interfaceType.ObjectType.Properties, ..traitProperties]);
        var boundType = new InterfaceType(interfaceType.Name, interfaceType.Constraints, objectType)
        {
            TraitMethodNames = traitMethodNames
        };

        return BindType(node, boundType);
    }

    private bool TrySubstituteGenericInterface(
        InterfaceInvocation node,
        GenericType generic,
        InterfaceType underlying,
        Type? expected,
        [MaybeNullWhen(false)] out InterfaceType substituted)
    {
        substituted = null;
        var substitution = node.TypeArguments != null
            ? ResolveExplicitInterfaceTypeArguments(node, generic)
            : _inferrer.InferInterfaceTypeArguments(node, generic, underlying, expected);

        if (substitution == null)
            return false;

        foreach (var tp in generic.Parameters)
        {
            if (tp.Constraint == null || !substitution.TryGetValue(tp, out var arg)) continue;
            if (!CheckTypeParameterConstraints(node, arg, tp))
                return false;
        }

        var substitutedObject = SubstituteObjectType(node, underlying.ObjectType, substitution);
        substituted = new InterfaceType(underlying.Name, underlying.Constraints, substitutedObject);
        return true;
    }

    private List<ObjectProperty> ResolveTraitProperties(List<DeclareFunctionSignature> signatures) =>
        signatures.ConvertAll(s => new ObjectProperty(false, s.Name.Text, Visit(s)));

    private List<ObjectProperty> ResolveInterfaceEvents(List<EventDeclaration> eventDeclarations) =>
        eventDeclarations.ConvertAll(e =>
            {
                MaybeVisit(e.Attributes);
                return new ObjectProperty(false, e.Name.Text, Visit(e));
            }
        );

    private List<ObjectProperty> ResolveInterfaceProperties(List<InterfaceType> constraints, List<PropertyDeclaration> propertyDeclarations)
    {
        var properties = new List<ObjectProperty>();
        foreach (var property in propertyDeclarations)
        {
            MaybeVisit(property.Attributes);

            var name = property.Name.Text;
            if (constraints.Find(i => i.GetProperty(name) != null) is { } subclass
                && !property.TryGetIntrinsicAttribute(_semanticModel, "override", out _))
            {
                _diagnostics.Error(property, InternalCodes.ConstraintPropertyOverride, $"Property '{name}' is already declared within constraint '{subclass}'.");
                return properties;
            }

            var isMutable = property.MutKeyword != null;
            var valueType = Visit(property.ColonTypeClause);

            if (property.Attributes != null)
                foreach (var attribute in property.Attributes.AttributeList)
                {
                    CheckPassiveDecorator(attribute);
                    CheckAttributeUsage(attribute, AttributeTargetsFlag.Property);
                }

            properties.Add(new ObjectProperty(isMutable, name, valueType));
        }

        return MergeOverloadedProperties(properties);
    }

    // A property name declared more than once, where every declaration is function-typed, is an overload set (e.g. CFrame.new's constructor shapes) merged into one IntersectionType.
    private static List<ObjectProperty> MergeOverloadedProperties(List<ObjectProperty> properties)
    {
        var merged = new List<ObjectProperty>();
        var indexByName = new Dictionary<string, int>();
        foreach (var property in properties)
        {
            if (indexByName.TryGetValue(property.Name, out var index)
                && property.ValueType is FunctionType newSignature
                && TryGetSignatures(merged[index].ValueType, out var existingSignatures))
            {
                existingSignatures.Add(newSignature);
                merged[index] = new ObjectProperty(merged[index].IsMutable, property.Name, new IntersectionType([..existingSignatures]));
                continue;
            }

            indexByName[property.Name] = merged.Count;
            merged.Add(property);
        }

        return merged;
    }

    private static bool TryGetSignatures(Type type, [MaybeNullWhen(false)] out List<FunctionType> signatures)
    {
        switch (type)
        {
            case FunctionType functionType:
                signatures = [functionType];
                return true;
            case IntersectionType intersection when intersection.Types.TrueForAll(t => t is FunctionType):
                signatures = intersection.Types.ConvertAll(t => (FunctionType)t);
                return true;
            default:
                signatures = null;
                return false;
        }
    }

    private ObjectIndexer? ResolveInterfaceIndexer(List<InterfaceType> constraints, IndexerDeclaration? indexerDeclaration)
    {
        if (indexerDeclaration == null)
            return null;

        var isMutable = indexerDeclaration.MutKeyword != null;
        var indexType = Visit(indexerDeclaration.IndexType);
        var valueType = Visit(indexerDeclaration.ColonTypeClause);
        if (constraints.Find(i => i.Indexer != null) is not { } subclass)
            return new ObjectIndexer(isMutable, indexType, valueType);

        _diagnostics.Error(indexerDeclaration, InternalCodes.ConstraintIndexerOverride, $"An indexer is already declared within constraint '{subclass}'.");
        return null;
    }

    private void CheckInterfaceInvocationInitializers(InterfaceInvocation node, InterfaceType interfaceType)
    {
        var objectType = interfaceType.ObjectType;
        var providedProperties = new HashSet<string>();
        foreach (var property in node.Body.Initializers.SelectMany(initializer => CheckInterfaceInvocationInitializer(interfaceType, initializer)))
            providedProperties.Add(property);

        foreach (var property in objectType.Properties.Where(property => !providedProperties.Contains(property.Name)))
            _diagnostics.Error(
                node.Body,
                InternalCodes.IncompleteInterfaceInvocation,
                $"Missing property initializer for '{property.Name}' in interface '{interfaceType.Name}'."
            );
    }

    private HashSet<string> CheckInterfaceInvocationInitializer(InterfaceType interfaceType, InterfaceInvocationInitializer initializer)
    {
        var providedProperties = new HashSet<string>();
        switch (initializer)
        {
            case PropertyInitializer propertyInitializer:
            {
                var propertyName = CheckPropertyInitializer(propertyInitializer, propertyInitializer.Name.Text, propertyInitializer.Expression, interfaceType);
                if (propertyName != null)
                    providedProperties.Add(propertyName);

                break;
            }
            case ShorthandPropertyInitializer shorthandPropertyInitializer:
                var shorthandPropertyName = CheckPropertyInitializer(
                    shorthandPropertyInitializer,
                    shorthandPropertyInitializer.Identifier.Name.Text,
                    shorthandPropertyInitializer.Identifier,
                    interfaceType
                );

                if (shorthandPropertyName != null)
                    providedProperties.Add(shorthandPropertyName);

                break;
            case IndexInitializer indexInitializer:
            {
                CheckIndexInitializer(indexInitializer, interfaceType);
                break;
            }
        }

        return providedProperties;
    }

    private string? CheckPropertyInitializer(Node node, string name, Expression expression, InterfaceType interfaceType)
    {
        var property = interfaceType.GetProperty(name);
        if (property == null)
        {
            _diagnostics.Error(
                node,
                InternalCodes.InvalidAccess,
                $"Property '{name}' does not exist on interface '{interfaceType.Name}'."
            );

            return null;
        }

        Check(expression, property.ValueType);
        return name;
    }

    private void CheckIndexInitializer(IndexInitializer initializer, InterfaceType interfaceType)
    {
        var indexer = interfaceType.Indexer;
        if (indexer == null)
        {
            _diagnostics.Error(
                initializer,
                InternalCodes.InvalidAccess,
                $"Interface '{interfaceType.Name}' does not have an indexer."
            );

            return;
        }

        Check(initializer.IndexExpression, indexer.KeyType);
        Check(initializer.Expression, indexer.ValueType);
    }
}