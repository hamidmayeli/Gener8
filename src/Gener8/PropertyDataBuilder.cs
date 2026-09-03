using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Gener8;

internal sealed class PropertyDataBuilder(
    INamedTypeSymbol? classSymbol,
    AttributeData? attribute,
    INamedTypeSymbol modelSymbol,
    RepositoryKind repositoryKind,
    IReadOnlyCollection<string>? qualifyingNamespaces = null,
    IReadOnlyCollection<string>? ignoredTypeMappings = null,
    string dtoSuffix = "Dto")
{
    private readonly List<INamedTypeSymbol> _autoTargetSymbols = [];
    private readonly List<string> _alreadyNullablePropertyNames = [];
    private readonly List<string> _iSetWithInitializerPropertyNames = [];

    public IReadOnlyCollection<INamedTypeSymbol> AutoTargetSymbols => _autoTargetSymbols;
    public IReadOnlyList<string> AlreadyNullablePropertyNames => _alreadyNullablePropertyNames;
    public IReadOnlyList<string> ISetWithInitializerPropertyNames => _iSetWithInitializerPropertyNames;

    public IReadOnlyCollection<PropertyData> GetProperties()
    {
        var ignoredNames = GetIgnoredProperties();
        var flattenNames = GetFlattenProperties();
        var flattenPrefix = GetFlattenPrefix();
        var includeInherited = GetIncludeInherited();
        var typeMappings = GetTypeMappings();
        PopulateInferredMappings(typeMappings, includeInherited);
        var renameMap = GetRenameMap();
        var forceNullableNames = GetForceNullableProperties();
        var existingDtoProps = GetExistingDtoPropertyNames();

        var ctorBackedNames = GetConstructorBackedPropertyNames(modelSymbol);
        var ctorBackedNameSet = new HashSet<string>(ctorBackedNames.Keys);

        var properties = new List<PropertyData>();
        foreach (var property in GetModelProperties(modelSymbol, includeInherited, ctorBackedNameSet))
        {
            if (ignoredNames.Contains(property.Name)) continue;

            if (flattenNames.Contains(property.Name))
            {
                if (property.Type is INamedTypeSymbol nestedType)
                {
                    var parentIsNullable = property.NullableAnnotation == NullableAnnotation.Annotated;
                    var parentTypeFullName = nestedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    foreach (var nested in GetModelProperties(nestedType, includeInherited: false))
                    {
                        var nestedName = flattenPrefix switch
                        {
                            FlattenPrefixMode.Parent => property.Name + nested.Name,
                            FlattenPrefixMode.Gaped => property.Name + "_" + nested.Name,
                            _ => nested.Name
                        };

                        var isUserDeclaredNested = existingDtoProps.Contains(nestedName);

                        properties.Add(
                            BuildPropertyData(
                                nested,
                                typeMappings,
                                renameMap: null,
                                repositoryKind,
                                nameOverride: nestedName,
                                flattenParent: (property.Name, parentTypeFullName, parentIsNullable),
                                isUserDeclared: isUserDeclaredNested
                                )
                            );
                    }
                }
            }
            else
            {
                var isForceNullable = forceNullableNames.Contains(property.Name);
                if (isForceNullable && property.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    _alreadyNullablePropertyNames.Add(property.Name);
                    isForceNullable = false;
                }

                // Collection-expression initializers (e.g. = []) adapt to any target type and are safe.
                // Only typed initializers (e.g. = new HashSet<T>()) are problematic.
                if (IsISetType(property.Type) && GetInitializer(property) is { } isetInit
                    && !isetInit.StartsWith("[", System.StringComparison.Ordinal))
                    _iSetWithInitializerPropertyNames.Add(property.Name);

                var dtoPropName = renameMap.TryGetValue(property.Name, out var renamed) ? renamed : property.Name;
                var isUserDeclared = existingDtoProps.Contains(dtoPropName);
                properties.Add(
                    BuildPropertyData(
                        property,
                        typeMappings,
                        renameMap,
                        repositoryKind,
                        isUserDeclared: isUserDeclared,
                        isConstructorBacked: ctorBackedNames.ContainsKey(property.Name),
                        isForceNullable: isForceNullable,
                        ctorDefaultInitializer: ctorBackedNames.TryGetValue(property.Name, out var ctorDefault) ? ctorDefault : null
                        )
                    );
            }
        }

        return properties;
    }

    // Scans model properties for complex types in qualifying namespaces and adds inferred
    // TypeMappings (e.g. Customer -> CustomerDto). Uses symbol identity to avoid overriding
    // explicit [TypeMapping] attributes, and the non-nullable key format for consistency.
    private void PopulateInferredMappings(Dictionary<string, string> typeMappings, bool includeInherited)
    {
        if (qualifyingNamespaces is null || qualifyingNamespaces.Count == 0) return;

        // Collect explicit TypeMapping source symbols for identity-based overlap detection.
        var explicitSourceSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (classSymbol is not null)
        {
            foreach (var a in classSymbol.GetAttributes())
            {
                if (a.AttributeClass?.ToDisplayString() != DefaultSource.TypeMappingAttribute.Name) continue;
                if (a.ConstructorArguments.Length < 2) continue;
                if (a.ConstructorArguments[0].Value is INamedTypeSymbol sourceType)
                    explicitSourceSymbols.Add(sourceType.OriginalDefinition);
            }
        }

        foreach (var property in GetModelProperties(modelSymbol, includeInherited))
            TryAddInferredMapping(property.Type, typeMappings, explicitSourceSymbols);
    }

    private void TryAddInferredMapping(
        ITypeSymbol type,
        Dictionary<string, string> typeMappings,
        HashSet<ISymbol> explicitSourceSymbols)
    {
        // Recurse into array element types (e.g. Product[] -> ProductDto).
        if (type is IArrayTypeSymbol arrayType)
        {
            TryAddInferredMapping(arrayType.ElementType, typeMappings, explicitSourceSymbols);
            return;
        }

        // Recurse into supported collection element types (e.g. List<Customer> -> CustomerDto).
        if (type is INamedTypeSymbol { IsGenericType: true, Arity: 1 } collType &&
            IsSupportedMappedCollection(collType))
        {
            TryAddInferredMapping(collType.TypeArguments[0], typeMappings, explicitSourceSymbols);
            return;
        }

        if (type is not INamedTypeSymbol { IsGenericType: false } namedType) return;
        if (namedType.TypeKind != TypeKind.Class) return;
        if (namedType.SpecialType != SpecialType.None) return; // skip string, object, etc.

        var ns = namedType.ContainingNamespace is { IsGlobalNamespace: false } nsSymbol
            ? nsSymbol.ToDisplayString()
            : "";

        if (!qualifyingNamespaces!.Contains(ns)) return;

        // Skip when an explicit [TypeMapping] already covers this type (symbol identity check).
        var originalDef = namedType.OriginalDefinition;
        if (explicitSourceSymbols.Contains(originalDef)) return;

        // Use the non-nullable key (same format as GetTypeMappings) to avoid duplicate entries.
        var key = namedType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
        if (typeMappings.ContainsKey(key)) return;
        if (ignoredTypeMappings?.Contains(key) == true) return;

        typeMappings[key] = namedType.Name + dtoSuffix;
        _autoTargetSymbols.Add((INamedTypeSymbol)originalDef);
    }

    private HashSet<string> GetExistingDtoPropertyNames()
    {
        var names = new HashSet<string>();
        if (classSymbol is null) return names;

        foreach (var member in classSymbol.GetMembers())
            if (member is IPropertySymbol prop && !prop.IsStatic)
                names.Add(prop.Name);

        return names;
    }

    private Dictionary<string, string> GetTypeMappings()
    {
        var typeMappings = new Dictionary<string, string>();
        if (classSymbol is null) return typeMappings;

        foreach (var a in classSymbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() != DefaultSource.TypeMappingAttribute.Name) continue;
            if (a.ConstructorArguments.Length < 2) continue;
            if (a.ConstructorArguments[0].Kind != TypedConstantKind.Type) continue;
            if (a.ConstructorArguments[1].Kind != TypedConstantKind.Type) continue;
            if (a.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType) continue;
            if (a.ConstructorArguments[1].Value is not INamedTypeSymbol targetType) continue;
            if (ignoredTypeMappings?.Contains(sourceType.ToDisplayString()) == true) continue;
            typeMappings[sourceType.ToDisplayString()] = targetType.ToDisplayString();
        }

        return typeMappings;
    }

    private HashSet<string> GetIgnoredProperties()
    {
        var ignoredNames = new HashSet<string>();
        if (attribute is null) return ignoredNames;

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key != "Ignore") continue;

            foreach (var item in namedArg.Value.Values)
                if (item.Value is string name)
                    ignoredNames.Add(name);
        }

        return ignoredNames;
    }

    private static PropertyData BuildPropertyData(
        IPropertySymbol property,
        Dictionary<string, string> typeMappings,
        Dictionary<string, string>? renameMap,
        RepositoryKind repositoryKind,
        string? nameOverride = null,
        (string Name, string TypeFullName, bool IsNullable)? flattenParent = null,
        bool isUserDeclared = false,
        bool isConstructorBacked = false,
        bool isForceNullable = false,
        string? ctorDefaultInitializer = null)
    {
        var name = nameOverride
            ?? (renameMap?.TryGetValue(property.Name, out var renamed) == true ? renamed : property.Name);

        var modelPropertyName = flattenParent is null && name != property.Name ? property.Name : null;

        // A get-only model property backed by a constructor needs init in the DTO so it can be populated.
        var isInitOnly = property.SetMethod is { IsInitOnly: true } || (property.SetMethod is null && isConstructorBacked);
        var hasSetter = property.SetMethod is not null && !property.SetMethod.IsInitOnly && !isInitOnly;

        // ForceNullable: capture the globally-qualified model type for the partial method stub.
        string? forceNullableModelType = null;
        if (isForceNullable)
            forceNullableModelType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new PropertyData(
            GetTypeData(property, typeMappings, repositoryKind, flattenParent?.IsNullable ?? false, isForceNullable),
            name,
            property.GetMethod is not null,
            hasSetter,
            isInitOnly,
            // ForceNullable suppresses required: a nullable DTO property cannot be required in a meaningful way.
            property.IsRequired && !(flattenParent?.IsNullable ?? false) && !isForceNullable,
            GetInitializer(property) ?? ctorDefaultInitializer,
            modelPropertyName,
            isUserDeclared,
            GetFlattenedData(property, flattenParent),
            isForceNullable,
            forceNullableModelType
            );
    }

    private static PropertyTypeData GetTypeData(
        IPropertySymbol property,
        Dictionary<string, string> typeMappings,
        RepositoryKind? repositoryKind,
        bool isParentNullable,
        bool forceNullable = false
        )
    {
        var originalType = property.Type.ToDisplayString();
        var isNullable = isParentNullable || property.NullableAnnotation == NullableAnnotation.Annotated || forceNullable;

        // Roslyn's default ToDisplayString() includes '?' for nullable reference types when
        // nullable context is enabled. TypeMapping keys are stored without '?' (from INamedTypeSymbol).
        // Normalise the lookup to find mappings for both nullable and non-nullable references.
        var typeForLookup = originalType.EndsWith("?")
            ? originalType.Substring(0, originalType.Length - 1)
            : originalType;

        var hasDirectTypeMapping = typeMappings.TryGetValue(originalType, out var mappedType)
            || typeMappings.TryGetValue(typeForLookup, out mappedType);

        var hasGenericTypeMapping = false;
        string? mappedCollectionElementType = null;
        string? typeMappedToDtoMethodName = null;
        var typeDisplay = hasDirectTypeMapping ? mappedType! : originalType;

        // When a TypeMapping is applied to a nullable source property, propagate nullability
        // to the mapped type so the generated DTO property is also nullable.
        if (hasDirectTypeMapping && isNullable && !typeDisplay.EndsWith("?"))
            typeDisplay += "?";

        if (hasDirectTypeMapping)
            typeMappedToDtoMethodName = ComputeTypeMappedMethodName(typeForLookup, mappedType!);

        if (!hasDirectTypeMapping && TryGetMappedCollectionType(property.Type, typeMappings, out var collectionTypeMapping))
        {
            typeDisplay = collectionTypeMapping.Value.TypeDisplay;
            mappedCollectionElementType = collectionTypeMapping.Value.ElementTypeDisplay;
            hasGenericTypeMapping = true;
            if (property.Type is INamedTypeSymbol { IsGenericType: true, Arity: 1 } collType)
                typeMappedToDtoMethodName = ComputeTypeMappedMethodName(
                    collType.TypeArguments[0].ToDisplayString(), mappedCollectionElementType);
        }

        // Handle T[] arrays: map element type and convert to List<TDto>.
        if (!hasDirectTypeMapping && !hasGenericTypeMapping
            && property.Type is IArrayTypeSymbol { ElementType: var arrElemType }
            && TryGetMappedCollectionElementType(arrElemType, typeMappings, out var mappedArrElem))
        {
            var baseList = $"System.Collections.Generic.List<{mappedArrElem}>";
            typeDisplay = isNullable ? baseList + "?" : baseList;
            hasGenericTypeMapping = true;
            typeMappedToDtoMethodName = ComputeTypeMappedMethodName(arrElemType.ToDisplayString(), mappedArrElem);
        }

        // ISet<T> with element TypeMapping → ToModel needs a cast since ISet<T> is not a valid
        // collection-expression target type in C#.
        string? toModelCastType = null;
        if (hasGenericTypeMapping && IsISetType(property.Type))
        {
            var originalElem = ((INamedTypeSymbol)property.Type).TypeArguments[0].ToDisplayString();
            toModelCastType = $"System.Collections.Generic.HashSet<{originalElem}>";
        }

        if ((isParentNullable || forceNullable) && !typeDisplay.EndsWith("?"))
            typeDisplay += "?";

        // For DynamoDB, remap abstract collection interfaces to List<T> so the SDK
        // can instantiate them. Only applies when there is no explicit TypeMapping override.
        var needsSpreadAssignment = false;
        if (!hasDirectTypeMapping && (repositoryKind == RepositoryKind.DynamoDb))
        {
            if (TryRemapToConcreteCollection(property.Type, mappedCollectionElementType, out var baseRemapped))
            {
                typeDisplay = isNullable ? baseRemapped + "?" : baseRemapped;
                needsSpreadAssignment = true;
            }
        }

        // True when the ? suffix represents Nullable<T> (a struct) rather than an NRT annotation.
        // Stripping ? from a value type changes the CLR type (DateTime? != DateTime), so it must
        // be preserved regardless of the consuming project's nullable reference type setting.
        var isNullableValueType = isNullable
            && !hasDirectTypeMapping
            && !hasGenericTypeMapping
            && !needsSpreadAssignment
            && property.Type.IsValueType;

        return new(
            typeDisplay,
            hasDirectTypeMapping || hasGenericTypeMapping,
            hasGenericTypeMapping,
            needsSpreadAssignment,
            IsEnumType(property),
            isNullable,
            GetEnumCollectionElementType(property),
            isNullableValueType,
            toModelCastType,
            typeMappedToDtoMethodName);
    }

    // Returns the element type display string when the property is a supported collection of enums
    // (e.g. "CategoryEnum" for IList<CategoryEnum>, "CategoryEnum?" for IList<CategoryEnum?>).
    // Returns null when not an enum collection.
    private static string? GetEnumCollectionElementType(IPropertySymbol property)
    {
        if (property.Type is not INamedTypeSymbol { IsGenericType: true, Arity: 1 } namedType)
            return null;
        if (!IsSupportedMappedCollection(namedType))
            return null;
        var elementType = namedType.TypeArguments[0];
        // IList<CategoryEnum>
        if (elementType.TypeKind == TypeKind.Enum)
            return elementType.ToDisplayString();
        // IList<CategoryEnum?> — element is Nullable<TEnum>
        if (elementType is INamedTypeSymbol { IsGenericType: true } elemNamed
            && elemNamed.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            && elemNamed.TypeArguments[0].TypeKind == TypeKind.Enum)
            return elemNamed.TypeArguments[0].ToDisplayString() + "?";
        return null;
    }

    private static bool IsEnumType(IPropertySymbol property)
    {
        if(property.NullableAnnotation != NullableAnnotation.Annotated)
            return property.Type.TypeKind == TypeKind.Enum;

        if (property.Type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = namedType.TypeArguments[0];
            return underlyingType.TypeKind == TypeKind.Enum;
        }

        return false;
    }

    private readonly record struct CollectionTypeMapping(string TypeDisplay, string ElementTypeDisplay);

    private static bool TryGetMappedCollectionType(
        ITypeSymbol type,
        Dictionary<string, string> typeMappings,
        [NotNullWhen(true)] out CollectionTypeMapping? collectionTypeMapping)
    {
        collectionTypeMapping = null;

        if (type is not INamedTypeSymbol { IsGenericType: true, Arity: 1 } namedType)
            return false;

        if (!IsSupportedMappedCollection(namedType))
            return false;

        if (!TryGetMappedCollectionElementType(namedType.TypeArguments[0], typeMappings, out var mappedElementType))
            return false;

        collectionTypeMapping = new(BuildGenericTypeDisplay(namedType, mappedElementType), mappedElementType);
        return true;
    }

    private static bool TryGetMappedCollectionElementType(
        ITypeSymbol type,
        Dictionary<string, string> typeMappings,
        [NotNullWhen(true)] out string? mappedElementType)
    {
        var originalType = type.ToDisplayString();
        if (typeMappings.TryGetValue(originalType, out mappedElementType))
            return true;

        // Also try without trailing '?' for the same reason as GetTypeData.
        if (originalType.EndsWith("?"))
        {
            var stripped = originalType.Substring(0, originalType.Length - 1);
            if (typeMappings.TryGetValue(stripped, out mappedElementType))
                return true;
        }

        if (TryGetMappedCollectionType(type, typeMappings, out var nestedCollectionType))
        {
            mappedElementType = nestedCollectionType.Value.TypeDisplay;
            return true;
        }

        mappedElementType = null;
        return false;
    }

    private static bool IsISetType(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } namedType
            && namedType.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.ISet<T>";

    private static bool IsSupportedMappedCollection(INamedTypeSymbol namedType)
        => namedType.ConstructedFrom.ToDisplayString() is
            "System.Collections.Generic.List<T>" or
            "System.Collections.Generic.IEnumerable<T>" or
            "System.Collections.Generic.ICollection<T>" or
            "System.Collections.Generic.IList<T>" or
            "System.Collections.Generic.IReadOnlyCollection<T>" or
            "System.Collections.Generic.IReadOnlyList<T>" or
            "System.Collections.Generic.ISet<T>" or
            "System.Collections.Generic.HashSet<T>";

    private static string BuildGenericTypeDisplay(INamedTypeSymbol namedType, string mappedElementType)
    {
        var constructedFromDisplay = namedType.ConstructedFrom.ToDisplayString();
        // ISet<T> cannot be instantiated; remap to the concrete HashSet<T> in the DTO.
        var genericTypeName = constructedFromDisplay == "System.Collections.Generic.ISet<T>"
            ? "System.Collections.Generic.HashSet"
            : constructedFromDisplay.Substring(0, constructedFromDisplay.IndexOf('<'));
        var nullableSuffix = namedType.NullableAnnotation == NullableAnnotation.Annotated ? "?" : string.Empty;
        return $"{genericTypeName}<{mappedElementType}>{nullableSuffix}";
    }

    private static FlattenedPropertyData? GetFlattenedData(
        IPropertySymbol property,
        (string Name, string TypeFullName, bool IsNullable)? flattenParent)
    {
        if (flattenParent is null)
            return null;

        var (parentName, parentFullTypeName, parentIsNullable) = flattenParent.Value;

        var readPath = parentIsNullable
            ? $"{parentName}?.{property.Name}"
            : $"{parentName}.{property.Name}";

        return new(
            readPath,
            parentName,
            parentFullTypeName,
            property.Name,
            property.Type.ToDisplayString().EndsWith("?")
        );
    }

    // Remaps IReadOnlyCollection<T>, IReadOnlyList<T>, IEnumerable<T>, IList<T>, ICollection<T>
    // to System.Collections.Generic.List<T>, and ISet<T> to System.Collections.Generic.HashSet<T>.
    // Returns true and sets baseRemapped (without any trailing '?') when a remapping is warranted;
    // caller handles nullability.
    private static bool TryRemapToConcreteCollection(
        ITypeSymbol type,
        string? mappedElementType,
        [NotNullWhen(true)] out string? baseRemapped)
    {
        baseRemapped = null;

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Interface, IsGenericType: true } namedType)
            return false;

        var constructedFromDisplay = namedType.ConstructedFrom.ToDisplayString();

        if (constructedFromDisplay == "System.Collections.Generic.ISet<T>")
        {
            var typeArg = mappedElementType ?? namedType.TypeArguments[0].ToDisplayString();
            baseRemapped = $"System.Collections.Generic.HashSet<{typeArg}>";
            return true;
        }

        var isCollectionInterface = constructedFromDisplay is
            "System.Collections.Generic.IReadOnlyCollection<T>" or
            "System.Collections.Generic.IReadOnlyList<T>" or
            "System.Collections.Generic.IEnumerable<T>" or
            "System.Collections.Generic.IList<T>" or
            "System.Collections.Generic.ICollection<T>";

        if (!isCollectionInterface) return false;

        var typeArgForList = mappedElementType ?? namedType.TypeArguments[0].ToDisplayString();
        baseRemapped = $"System.Collections.Generic.List<{typeArgForList}>";
        return true;
    }

    private static IEnumerable<IPropertySymbol> GetModelProperties(
        INamedTypeSymbol modelSymbol, bool includeInherited, HashSet<string>? ctorBackedNames = null)
    {
        var seenNames = new HashSet<string>();
        var current = modelSymbol;

        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol prop) continue;
                if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                if (prop.IsStatic) continue;
                if (prop.SetMethod is null && !(ctorBackedNames?.Contains(prop.Name) ?? false)) continue;
                if (!seenNames.Add(prop.Name)) continue;
                yield return prop;
            }
            if (!includeInherited) break;
            current = current.BaseType;
        }
    }

    // Returns a map of public property names → their constructor parameter default value text
    // for properties covered by a non-implicit constructor (PascalCase exact match or
    // capitalize-first for camelCase params). Used to include get-only model properties
    // that can only be set via that constructor, and to propagate default values to the DTO.
    private static Dictionary<string, string?> GetConstructorBackedPropertyNames(INamedTypeSymbol modelSymbol)
    {
        var propNames = new HashSet<string>();
        foreach (var member in modelSymbol.GetMembers())
            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } p)
                propNames.Add(p.Name);

        foreach (var ctor in modelSymbol.InstanceConstructors)
        {
            if (ctor.IsImplicitlyDeclared) continue;
            if (ctor.Parameters.Length == 0) continue;

            var result = new Dictionary<string, string?>();
            var allMatch = true;
            foreach (var param in ctor.Parameters)
            {
                if (propNames.Contains(param.Name))
                {
                    result[param.Name] = GetCtorParameterDefault(param);
                }
                else
                {
                    var cap = param.Name.Length > 0
                        ? char.ToUpper(param.Name[0]) + param.Name.Substring(1)
                        : param.Name;
                    if (propNames.Contains(cap))
                        result[cap] = GetCtorParameterDefault(param);
                    else
                    {
                        allMatch = false;
                        break;
                    }
                }
            }

            if (allMatch) return result;
        }

        return [];
    }

    private static string? GetCtorParameterDefault(IParameterSymbol param)
    {
        foreach (var syntaxRef in param.DeclaringSyntaxReferences)
            if (syntaxRef.GetSyntax() is ParameterSyntax paramSyntax && paramSyntax.Default is not null)
                return paramSyntax.Default.Value.ToString();
        return null;
    }

    private bool GetIncludeInherited()
    {
        if (attribute is null) return false;
        foreach (var namedArg in attribute.NamedArguments)
            if (namedArg.Key == "IncludeInherited" && namedArg.Value.Value is bool val)
                return val;

        return false;
    }

    private HashSet<string> GetFlattenProperties()
    {
        var result = new HashSet<string>();
        if (attribute is null) return result;
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key != "Flatten") continue;
            foreach (var item in namedArg.Value.Values)
                if (item.Value is string name)
                    result.Add(name);
        }
        return result;
    }

    private HashSet<string> GetForceNullableProperties()
    {
        var result = new HashSet<string>();
        if (attribute is null) return result;
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key != "ForceNullable") continue;
            foreach (var item in namedArg.Value.Values)
                if (item.Value is string name)
                    result.Add(name);
        }
        return result;
    }

    private FlattenPrefixMode GetFlattenPrefix()
    {
        if (attribute is null) return FlattenPrefixMode.Parent;
        foreach (var namedArg in attribute.NamedArguments)
            if (namedArg.Key == "FlattenPrefix" && namedArg.Value.Value is int val)
                return (FlattenPrefixMode)val;
        return FlattenPrefixMode.Parent;
    }

    private Dictionary<string, string> GetRenameMap()
    {
        var map = new Dictionary<string, string>();
        if (classSymbol is null) return map;
        foreach (var a in classSymbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() != DefaultSource.RenamePropertyAttribute.Name) continue;
            if (a.ConstructorArguments.Length < 2) continue;
            if (a.ConstructorArguments[0].Value is not string sourceName) continue;
            if (a.ConstructorArguments[1].Value is not string targetName) continue;
            map[sourceName] = targetName;
        }
        return map;
    }

    private static string? GetInitializer(IPropertySymbol prop)
    {
        if (prop.DeclaringSyntaxReferences.Length > 0
            && prop.DeclaringSyntaxReferences[0].GetSyntax() is PropertyDeclarationSyntax propSyntax
            && propSyntax.Initializer is not null)
            return propSyntax.Initializer.Value.ToString();

        return null;
    }

    // Extracts the simple (unqualified) type name from a fully-qualified display string.
    private static string GetSimpleName(string displayName)
    {
        var lastDot = displayName.LastIndexOf('.');
        return lastDot >= 0 ? displayName.Substring(lastDot + 1) : displayName;
    }

    // Computes the method name to call on the target DTO type given the source model type.
    // Mirrors SyntaxTransformer.ComputeToDtoMethodName but works with fully-qualified display strings.
    private static string ComputeTypeMappedMethodName(string sourceTypeDisplay, string targetDtoDisplay)
    {
        var src = sourceTypeDisplay.EndsWith("?") ? sourceTypeDisplay.Substring(0, sourceTypeDisplay.Length - 1) : sourceTypeDisplay;
        var tgt = targetDtoDisplay.EndsWith("?") ? targetDtoDisplay.Substring(0, targetDtoDisplay.Length - 1) : targetDtoDisplay;
        var modelSimple = GetSimpleName(src);
        var dtoSimple = GetSimpleName(tgt);
        if (dtoSimple.Length > modelSimple.Length && dtoSimple.StartsWith(modelSimple, System.StringComparison.Ordinal))
            return "To" + dtoSimple.Substring(modelSimple.Length);
        return "ToDto";
    }
}
