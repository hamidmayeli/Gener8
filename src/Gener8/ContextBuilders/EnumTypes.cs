using Microsoft.CodeAnalysis;

namespace Gener8.ContextBuilders;

// Enum classification for DTO properties. Repository integrations emit converter attributes
// based on these, so the "is this an enum" question is answered in one place.
internal static class EnumTypes
{
    public static bool IsEnum(IPropertySymbol property)
    {
        if (property.NullableAnnotation != NullableAnnotation.Annotated)
            return property.Type.TypeKind == TypeKind.Enum;

        return TryGetNullableUnderlyingEnum(property.Type, out _);
    }

    // Returns the element type display string when the property is a supported collection of
    // enums (e.g. "CategoryEnum" for IList<CategoryEnum>, "CategoryEnum?" for IList<CategoryEnum?>).
    // Returns null when not an enum collection.
    public static string? CollectionElementType(IPropertySymbol property)
    {
        if (property.Type is not INamedTypeSymbol { IsGenericType: true, Arity: 1 } namedType)
            return null;

        if (!KnownCollections.IsMappable(namedType))
            return null;

        var elementType = namedType.TypeArguments[0];

        // IList<CategoryEnum>
        if (elementType.TypeKind == TypeKind.Enum)
            return elementType.ToDisplayString();

        // IList<CategoryEnum?> — element is Nullable<TEnum>
        if (TryGetNullableUnderlyingEnum(elementType, out var underlyingEnum))
            return underlyingEnum.ToDisplayString() + "?";

        return null;
    }

    private static bool TryGetNullableUnderlyingEnum(ITypeSymbol type, out ITypeSymbol underlyingEnum)
    {
        underlyingEnum = null!;

        if (type is not INamedTypeSymbol { IsGenericType: true } namedType) return false;
        if (namedType.ConstructedFrom.SpecialType != SpecialType.System_Nullable_T) return false;
        if (namedType.TypeArguments[0].TypeKind != TypeKind.Enum) return false;

        underlyingEnum = namedType.TypeArguments[0];
        return true;
    }
}
