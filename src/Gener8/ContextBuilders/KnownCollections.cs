using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.ContextBuilders;

// The single source of truth for the collection types the generator understands.
// Previously these type-name lists were duplicated across four separate predicates.
internal static class KnownCollections
{
    public const string SetInterfaceDefinition = "System.Collections.Generic.ISet<T>";
    public const string HashSetName = "System.Collections.Generic.HashSet";
    public const string ListName = "System.Collections.Generic.List";

    // Generic collection definitions whose single type argument participates in element type mapping.
    private static readonly HashSet<string> _mappable =
    [
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.IEnumerable<T>",
        "System.Collections.Generic.ICollection<T>",
        "System.Collections.Generic.IList<T>",
        "System.Collections.Generic.IReadOnlyCollection<T>",
        "System.Collections.Generic.IReadOnlyList<T>",
        SetInterfaceDefinition,
        "System.Collections.Generic.HashSet<T>",
    ];

    // Abstract collection interfaces a repository SDK cannot instantiate; remapped to List<T>.
    // ISet<T> is handled separately because it remaps to HashSet<T> rather than List<T>.
    private static readonly HashSet<string> _remappableInterfaces =
    [
        "System.Collections.Generic.IReadOnlyCollection<T>",
        "System.Collections.Generic.IReadOnlyList<T>",
        "System.Collections.Generic.IEnumerable<T>",
        "System.Collections.Generic.IList<T>",
        "System.Collections.Generic.ICollection<T>",
    ];

    public static bool IsSet(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } namedType && IsSet(namedType);

    public static bool IsSet(INamedTypeSymbol namedType)
        => Definition(namedType) == SetInterfaceDefinition;

    // True when the type is a single-argument collection whose element type can be remapped.
    public static bool IsMappable(INamedTypeSymbol namedType)
        => _mappable.Contains(Definition(namedType));

    // True when the type is an abstract collection interface that needs a concrete DTO equivalent.
    public static bool IsRemappableInterface(INamedTypeSymbol namedType)
        => _remappableInterfaces.Contains(Definition(namedType));

    // The instantiable generic type name (without type arguments) to use in the DTO.
    // ISet<T> cannot be instantiated, so it becomes HashSet.
    public static string ConcreteGenericName(INamedTypeSymbol namedType)
    {
        var definition = Definition(namedType);
        return definition == SetInterfaceDefinition
            ? HashSetName
            : definition.Substring(0, definition.IndexOf('<'));
    }

    public static string Set(string elementType) => $"{HashSetName}<{elementType}>";

    public static string List(string elementType) => $"{ListName}<{elementType}>";

    private static string Definition(INamedTypeSymbol namedType)
        => namedType.ConstructedFrom.ToDisplayString();
}
