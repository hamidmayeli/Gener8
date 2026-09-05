using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Gener8.ContextBuilders;

// Some repository SDKs materialise DTO instances themselves and cannot instantiate an abstract
// collection interface, so those properties get a concrete type in the DTO:
// IReadOnlyCollection<T>/IReadOnlyList<T>/IEnumerable<T>/IList<T>/ICollection<T> -> List<T>,
// and ISet<T> -> HashSet<T>.
// Whether a backend needs this is RepositoryProfile.RemapsAbstractCollections; this type only
// knows how to pick the concrete equivalent.
internal static class RepositoryCollectionRemapper
{
    // Returns the concrete type display without any trailing '?'; the caller applies nullability.
    // mappedElementType, when non-null, is a already-remapped element type to preserve.
    public static bool TryRemap(
        ITypeSymbol type,
        string? mappedElementType,
        [NotNullWhen(true)] out string? concreteType)
    {
        concreteType = null;

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Interface, IsGenericType: true } namedType)
            return false;

        var elementType = mappedElementType ?? namedType.TypeArguments[0].ToDisplayString();

        if (KnownCollections.IsSet(namedType))
            concreteType = KnownCollections.Set(elementType);
        else if (KnownCollections.IsRemappableInterface(namedType))
            concreteType = KnownCollections.List(elementType);

        return concreteType is not null;
    }
}
