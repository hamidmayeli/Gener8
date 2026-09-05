using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Gener8.ContextBuilders;

// Some repository SDKs materialise DTO instances themselves and cannot instantiate an abstract
// collection interface, so those properties get a concrete type in the DTO:
// IReadOnlyCollection<T>/IReadOnlyList<T>/IEnumerable<T>/IList<T>/ICollection<T> -> List<T>,
// ISet<T> -> HashSet<T>, IDictionary<K,V>/IReadOnlyDictionary<K,V> -> Dictionary<K,V>.
// Whether a backend needs this is RepositoryProfile.RemapsAbstractCollections; this type only
// knows how to pick the concrete equivalent.
internal static class RepositoryCollectionRemapper
{
    // Returns the concrete type display without any trailing '?'; the caller applies nullability.
    // mappedElementType, when non-null, is an already-remapped value type to preserve.
    // mappedKeyType, when non-null, is an already-remapped key type to preserve (dictionary only).
    public static bool TryRemap(
        ITypeSymbol type,
        string? mappedElementType,
        string? mappedKeyType,
        [NotNullWhen(true)] out string? concreteType)
    {
        concreteType = null;

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Interface, IsGenericType: true } namedType)
            return false;

        // Single-argument collection interfaces -> List<T> or HashSet<T>.
        if (namedType.Arity == 1)
        {
            var elementType = mappedElementType ?? namedType.TypeArguments[0].ToDisplayString();

            if (KnownCollections.IsSet(namedType))
                concreteType = KnownCollections.Set(elementType);
            else if (KnownCollections.IsRemappableInterface(namedType))
                concreteType = KnownCollections.List(elementType);

            return concreteType is not null;
        }

        // Two-argument dictionary interfaces -> Dictionary<K,V>.
        if (namedType.Arity == 2 && KnownCollections.IsRemappableDictionaryInterface(namedType))
        {
            var keyType = mappedKeyType ?? namedType.TypeArguments[0].ToDisplayString();
            var valueType = mappedElementType ?? namedType.TypeArguments[1].ToDisplayString();
            concreteType = KnownCollections.Dictionary(keyType, valueType);
            return true;
        }

        return false;
    }
}
