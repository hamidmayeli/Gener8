using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Gener8.ContextBuilders;

// Owns model type → DTO type mapping: the explicit [TypeMapping] attributes, the mappings
// inferred from DtoNamespaces, and the lookups that consume them.
//
// Type identity is keyed on the non-nullable display string. Roslyn's default ToDisplayString()
// includes '?' for nullable reference types when the nullable context is enabled, so every
// lookup normalises the trailing '?' away before probing the map.
internal sealed class TypeMappingResolver
{
    private readonly Dictionary<string, string> _mappings;
    private readonly HashSet<ISymbol> _explicitSources;
    private readonly List<AutoDtoTarget> _autoTargets = [];
    private readonly INamedTypeSymbol _modelSymbol;
    private readonly IReadOnlyCollection<string>? _qualifyingNamespaces;
    private readonly IReadOnlyCollection<string>? _ignoredTypeMappings;
    private readonly string _dtoSuffix;

    public TypeMappingResolver(
        INamedTypeSymbol? classSymbol,
        INamedTypeSymbol modelSymbol,
        IReadOnlyCollection<string>? qualifyingNamespaces,
        IReadOnlyCollection<string>? ignoredTypeMappings,
        string dtoSuffix)
    {
        _modelSymbol = modelSymbol;
        _qualifyingNamespaces = qualifyingNamespaces;
        _ignoredTypeMappings = ignoredTypeMappings;
        _dtoSuffix = dtoSuffix;
        _mappings = ReadExplicitMappings(classSymbol, ignoredTypeMappings, out _explicitSources);
    }

    // Model types in a qualifying namespace that need a DTO synthesised for them.
    public IReadOnlyCollection<AutoDtoTarget> AutoTargets => _autoTargets;

    // Scans model properties for complex types in qualifying namespaces and adds inferred
    // mappings (e.g. Customer -> CustomerDto), skipping types already covered by an explicit
    // [TypeMapping] (compared by symbol identity, not by name).
    public void AddInferredMappings(
        bool includeInherited,
        OnlyIncludeFilter onlyInclude,
        ICollection<string>? constructorBackedNames)
    {
        if (_qualifyingNamespaces is null || _qualifyingNamespaces.Count == 0) return;

        // Must mirror the property set the DTO is built from — including get-only properties
        // reachable through a constructor — or those properties keep the model type in the DTO
        // and no companion DTO is generated for it.
        foreach (var property in ModelPropertyReader.ReadSettable(_modelSymbol, includeInherited, constructorBackedNames))
        {
            if (!onlyInclude.Includes(property.Name)) continue;

            TryAddInferredMapping(property.Type, onlyInclude.SubPathsFor(property.Name));
        }
    }

    // Direct mapping for the property's own type.
    public bool TryGetMapping(ITypeSymbol type, [NotNullWhen(true)] out string? mappedType)
        => TryGetMapping(type.ToDisplayString(), out mappedType);

    public bool TryGetMapping(string typeDisplay, [NotNullWhen(true)] out string? mappedType)
        => _mappings.TryGetValue(typeDisplay, out mappedType)
            || _mappings.TryGetValue(TypeNames.StripNullable(typeDisplay), out mappedType);

    // Mapping for a collection's element type, recursing through nested collections
    // (e.g. List<List<Customer>> -> List<List<CustomerDto>>).
    public bool TryGetElementMapping(ITypeSymbol type, [NotNullWhen(true)] out string? mappedElementType)
    {
        if (TryGetMapping(type, out mappedElementType))
            return true;

        if (TryGetCollectionMapping(type, out var nestedCollection))
        {
            mappedElementType = nestedCollection.TypeDisplay;
            return true;
        }

        mappedElementType = null;
        return false;
    }

    // Mapping for a supported single-argument collection whose element type is remapped.
    public bool TryGetCollectionMapping(ITypeSymbol type, out CollectionTypeMapping collectionMapping)
    {
        collectionMapping = default;

        if (type is not INamedTypeSymbol { IsGenericType: true, Arity: 1 } namedType)
            return false;

        if (!KnownCollections.IsMappable(namedType))
            return false;

        if (!TryGetElementMapping(namedType.TypeArguments[0], out var mappedElementType))
            return false;

        collectionMapping = new(BuildGenericTypeDisplay(namedType, mappedElementType), mappedElementType);
        return true;
    }

    private static string BuildGenericTypeDisplay(INamedTypeSymbol namedType, string mappedElementType)
    {
        var display = $"{KnownCollections.ConcreteGenericName(namedType)}<{mappedElementType}>";
        return TypeNames.WithNullable(display, namedType.NullableAnnotation == NullableAnnotation.Annotated);
    }

    // Single pass over the [TypeMapping] attributes, producing both the mapping table and the
    // set of explicitly-mapped source symbols. The symbol set intentionally records ignored
    // mappings too: [IgnoreTypeMapping] suppresses the mapping, not the fact that the user
    // spoke about that type.
    private static Dictionary<string, string> ReadExplicitMappings(
        INamedTypeSymbol? classSymbol,
        IReadOnlyCollection<string>? ignoredTypeMappings,
        out HashSet<ISymbol> explicitSources)
    {
        var mappings = new Dictionary<string, string>();
        explicitSources = new(SymbolEqualityComparer.Default);

        foreach (var (source, target) in AttributeReader.GetTypePairs(classSymbol, DefaultSource.TypeMappingAttribute.Name))
        {
            explicitSources.Add(source.OriginalDefinition);

            var key = source.ToDisplayString();
            if (ignoredTypeMappings?.Contains(key) == true) continue;

            mappings[key] = target.ToDisplayString();
        }

        return mappings;
    }

    private void TryAddInferredMapping(ITypeSymbol type, IReadOnlyCollection<string>? subPaths)
    {
        // Recurse into array element types (e.g. Product[] -> ProductDto).
        if (type is IArrayTypeSymbol arrayType)
        {
            TryAddInferredMapping(arrayType.ElementType, subPaths);
            return;
        }

        // Recurse into supported collection element types (e.g. List<Customer> -> CustomerDto).
        if (type is INamedTypeSymbol { IsGenericType: true, Arity: 1 } collectionType
            && KnownCollections.IsMappable(collectionType))
        {
            TryAddInferredMapping(collectionType.TypeArguments[0], subPaths);
            return;
        }

        if (type is not INamedTypeSymbol { IsGenericType: false } namedType) return;
        if (namedType.TypeKind != TypeKind.Class) return;
        if (namedType.SpecialType != SpecialType.None) return;  // skip string, object, etc.

        var containingNamespace = namedType.ContainingNamespace is { IsGlobalNamespace: false } namespaceSymbol
            ? namespaceSymbol.ToDisplayString()
            : "";

        if (!_qualifyingNamespaces!.Contains(containingNamespace)) return;

        // Skip when an explicit [TypeMapping] already covers this type (symbol identity check).
        var originalDefinition = namedType.OriginalDefinition;
        if (_explicitSources.Contains(originalDefinition)) return;

        // Use the non-nullable key (same format as the explicit mappings) to avoid duplicates.
        var key = namedType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
        if (_mappings.ContainsKey(key)) return;
        if (_ignoredTypeMappings?.Contains(key) == true) return;

        _mappings[key] = namedType.Name + _dtoSuffix;
        _autoTargets.Add(new((INamedTypeSymbol)originalDefinition, subPaths));
    }
}
