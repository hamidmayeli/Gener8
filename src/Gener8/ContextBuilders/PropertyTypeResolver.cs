using Gener8.ContextBuilders.TypeMapping;
using Gener8.Contexts;
using Microsoft.CodeAnalysis;

namespace Gener8.ContextBuilders;

// Turns a model property's type into the DTO-side type description.
//
// Mapping recognition is delegated to an ordered rule list; this class only layers on the
// concerns that apply regardless of which rule matched: nullability, the ISet<T> cast needed
// by ToModel, and the repository's concrete-collection/dictionary requirement.
internal sealed class PropertyTypeResolver(TypeMappingResolver mappings, RepositoryProfile repository)
{
    private static readonly ITypeMappingRule[] _rules =
    [
        new DirectMappingRule(),
        new CollectionMappingRule(),
        new ArrayMappingRule(),
        new DictionaryMappingRule(),
    ];

    public PropertyTypeData Resolve(IPropertySymbol property, bool isParentNullable, bool forceNullable)
    {
        var originalTypeDisplay = property.Type.ToDisplayString();
        var isNullable = isParentNullable
            || property.NullableAnnotation == NullableAnnotation.Annotated
            || forceNullable;

        var mapped = ApplyRules(new(property.Type, originalTypeDisplay, isNullable, mappings));

        var hasDirectMapping = mapped is { IsGeneric: false };
        var hasGenericMapping = mapped is { IsGeneric: true };

        // Parent or forced nullability is additive for an unmapped type; a mapped type already
        // had it applied by the rule.
        var typeDisplay = mapped?.TypeDisplay
            ?? TypeNames.WithNullable(originalTypeDisplay, isParentNullable || forceNullable);

        // A mapped element type makes ToModel need a cast for ISet<T>, which is not a valid
        // collection-expression target type in C#.
        string? toModelCastType = null;
        if (hasGenericMapping && KnownCollections.IsSet(property.Type))
            toModelCastType = KnownCollections.Set(
                ((INamedTypeSymbol)property.Type).TypeArguments[0].ToDisplayString());

        // Whether the property's original type is a known dictionary.
        var isDictionary = property.Type is INamedTypeSymbol { IsGenericType: true, Arity: 2 } dt
            && KnownCollections.IsKnownDictionary(dt);

        // An explicit mapping of the property's own type wins over the repository remap.
        var needsSpreadAssignment = false;
        if (!hasDirectMapping
            && repository.RemapsAbstractCollections
            && RepositoryCollectionRemapper.TryRemap(
                property.Type, mapped?.ElementType, mapped?.DictionaryMappedKeyType, out var concreteType))
        {
            typeDisplay = TypeNames.WithNullable(concreteType, isNullable);
            needsSpreadAssignment = true;
        }

        return new(
            typeDisplay,
            hasDirectMapping || hasGenericMapping,
            hasGenericMapping,
            needsSpreadAssignment,
            EnumTypes.IsEnum(property),
            isNullable,
            EnumTypes.CollectionElementType(property),
            IsNullableValueType(property, isNullable, mapped, needsSpreadAssignment),
            toModelCastType,
            mapped?.ToDtoMethodName,
            isDictionary,
            mapped?.DictionaryKeyToDtoMethodName);
    }

    // True when the '?' suffix represents Nullable<T> (a struct) rather than an NRT annotation.
    // Stripping '?' from a value type changes the CLR type (DateTime? != DateTime), so it must
    // be preserved regardless of the consuming project's nullable reference type setting.
    private static bool IsNullableValueType(
        IPropertySymbol property, bool isNullable, MappedType? mapped, bool needsSpreadAssignment)
        => isNullable
            && mapped is null
            && !needsSpreadAssignment
            && property.Type.IsValueType;

    private static MappedType? ApplyRules(in TypeMappingContext context)
    {
        foreach (var rule in _rules)
            if (rule.TryApply(context, out var mapped))
                return mapped;

        return null;
    }
}
