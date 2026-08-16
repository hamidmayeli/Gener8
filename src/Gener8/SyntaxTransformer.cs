using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Gener8;

internal static class SyntaxTransformer
{
    public static bool IsPartialClassWithAttributes(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls) return false;
        if (cls.AttributeLists.Count == 0) return false;
        return cls.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    public static ClassTarget? ExtractClassTarget(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol classSymbol)
            return null;

        if (!TryGetFromModelAttributeData(classSymbol, out AttributeData? attr)) return null;
        if (!TryGetModelSymbol(attr, out INamedTypeSymbol? modelSymbol)) return null;

        var ns = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } nsSymbol
            ? nsSymbol.ToDisplayString()
            : null;

        var accessibility = classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal"
        };

        var ignoredNames = GetIgnoredProperties(attr);
        var flattenNames = GetFlattenProperties(attr);
        var flattenPrefix = GetFlattenPrefix(attr);
        var includeInherited = GetIncludeInherited(attr);
        var typeMappings = GetTypeMappings(classSymbol);
        var renameMap = GetRenameMap(classSymbol);

        var modelFullName = modelSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var existingDtoProps = GetExistingDtoPropertyNames(classSymbol);
        var repositoryKind = GetRepositoryKind(attr);

        var properties = new List<PropertyData>();
        foreach (var prop in GetModelProperties(modelSymbol, includeInherited))
        {
            if (ignoredNames.Contains(prop.Name)) continue;

            if (flattenNames.Contains(prop.Name))
            {
                if (prop.Type is INamedTypeSymbol nestedType)
                {
                    var parentIsNullable = prop.NullableAnnotation == NullableAnnotation.Annotated;
                    var parentTypeFullName = nestedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    foreach (var nested in GetModelProperties(nestedType, includeInherited: false))
                    {
                        var nestedName = flattenPrefix switch
                        {
                            FlattenPrefixMode.Parent => prop.Name + nested.Name,
                            FlattenPrefixMode.Gaped => prop.Name + "_" + nested.Name,
                            _ => null
                        };
                        var isUserDeclaredNested = existingDtoProps.Contains(nestedName ?? nested.Name);
                        properties.Add(BuildPropertyData(
                            nested, typeMappings, renameMap: null, repositoryKind,
                            nameOverride: nestedName, parentIsNullable: parentIsNullable,
                            flattenParentName: prop.Name, flattenParentTypeFullName: parentTypeFullName,
                            isUserDeclared: isUserDeclaredNested));
                    }
                }
                continue;
            }

            var dtoPropName = renameMap.TryGetValue(prop.Name, out var renamed) ? renamed : prop.Name;
            var isUserDeclared = existingDtoProps.Contains(dtoPropName);
            properties.Add(BuildPropertyData(prop, typeMappings, renameMap, repositoryKind, isUserDeclared: isUserDeclared));
        }

        return new ClassTarget(classSymbol.Name, ns, accessibility, properties, modelFullName, modelSymbol.Name, repositoryKind);
    }

    private static HashSet<string> GetExistingDtoPropertyNames(INamedTypeSymbol classSymbol)
    {
        var names = new HashSet<string>();
        foreach (var member in classSymbol.GetMembers())
            if (member is IPropertySymbol prop && !prop.IsStatic)
                names.Add(prop.Name);
        return names;
    }

    private static Dictionary<string, string> GetTypeMappings(INamedTypeSymbol classSymbol)
    {
        var typeMappings = new Dictionary<string, string>();
        foreach (var a in classSymbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() != DefaultSource.TypeMappingAttribute.Name) continue;
            if (a.ConstructorArguments.Length < 2) continue;
            if (a.ConstructorArguments[0].Kind != TypedConstantKind.Type) continue;
            if (a.ConstructorArguments[1].Kind != TypedConstantKind.Type) continue;
            if (a.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType) continue;
            if (a.ConstructorArguments[1].Value is not INamedTypeSymbol targetType) continue;
            typeMappings[sourceType.ToDisplayString()] = targetType.ToDisplayString();
        }

        return typeMappings;
    }

    private static HashSet<string> GetIgnoredProperties(AttributeData attr)
    {
        var ignoredNames = new HashSet<string>();
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key != "Ignore") continue;

            foreach (var item in namedArg.Value.Values)
                if (item.Value is string name)
                    ignoredNames.Add(name);
        }

        return ignoredNames;
    }

    private static PropertyData BuildPropertyData(
        IPropertySymbol prop,
        Dictionary<string, string> typeMappings,
        Dictionary<string, string>? renameMap,
        RepositoryKind repositoryKind,
        string? nameOverride = null,
        bool parentIsNullable = false,
        string? flattenParentName = null,
        string? flattenParentTypeFullName = null,
        bool isUserDeclared = false)
    {
        var originalType = prop.Type.ToDisplayString();
        var hasTypeMapping = typeMappings.ContainsKey(originalType);
        var typeDisplay = hasTypeMapping ? typeMappings[originalType] : originalType;

        // Capture type before applying parent nullability so we can track whether the
        // nested property was originally nullable (vs. made nullable by the parent).
        var typeBeforeParentNullability = typeDisplay;
        if (parentIsNullable && !typeDisplay.EndsWith("?"))
            typeDisplay += "?";

        var flattenedOriginallyNullable = typeBeforeParentNullability.EndsWith("?");

        // For DynamoDB/MongoDB, remap abstract collection interfaces to List<T> so the SDK
        // can instantiate them. Only applies when there is no explicit TypeMapping override.
        var needsSpreadAssignment = false;
        if (!hasTypeMapping && (repositoryKind == RepositoryKind.DynamoDb))
        {
            var isNullable = typeDisplay.EndsWith("?");
            if (TryRemapToConcreteCollection(prop.Type, out var baseRemapped) && baseRemapped is not null)
            {
                typeDisplay = isNullable ? baseRemapped + "?" : baseRemapped;
                needsSpreadAssignment = true;
            }
        }

        var name = nameOverride
            ?? (renameMap is not null && renameMap.TryGetValue(prop.Name, out var renamed) ? renamed : prop.Name);

        var modelPropertyName = flattenParentName is null && name != prop.Name ? prop.Name : null;

        var flattenedReadPath = flattenParentName is not null
            ? (parentIsNullable ? $"{flattenParentName}?.{prop.Name}" : $"{flattenParentName}.{prop.Name}")
            : null;

        return new PropertyData(
            typeDisplay, name,
            prop.GetMethod is not null,
            prop.SetMethod is not null && !prop.SetMethod.IsInitOnly,
            prop.SetMethod is { IsInitOnly: true },
            prop.IsRequired && !parentIsNullable,
            GetInitializer(prop),
            modelPropertyName,
            flattenedReadPath,
            hasTypeMapping,
            isUserDeclared,
            needsSpreadAssignment,
            flattenParentName,
            flattenParentTypeFullName,
            flattenParentName is not null ? prop.Name : null,
            flattenedOriginallyNullable);
    }

    // Remaps IReadOnlyCollection<T>, IReadOnlyList<T>, IEnumerable<T>, IList<T>, ICollection<T>
    // to System.Collections.Generic.List<T>. Returns true and sets baseRemapped (without any
    // trailing '?') when a remapping is warranted; caller handles nullability.
    private static bool TryRemapToConcreteCollection(ITypeSymbol type, out string? baseRemapped)
    {
        baseRemapped = null;

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Interface, IsGenericType: true } namedType)
            return false;

        var constructedFromDisplay = namedType.ConstructedFrom.ToDisplayString();
        var isCollectionInterface = constructedFromDisplay is
            "System.Collections.Generic.IReadOnlyCollection<T>" or
            "System.Collections.Generic.IReadOnlyList<T>" or
            "System.Collections.Generic.IEnumerable<T>" or
            "System.Collections.Generic.IList<T>" or
            "System.Collections.Generic.ICollection<T>";

        if (!isCollectionInterface) return false;

        var typeArg = namedType.TypeArguments[0].ToDisplayString();
        baseRemapped = $"System.Collections.Generic.List<{typeArg}>";
        return true;
    }

    private static IEnumerable<IPropertySymbol> GetModelProperties(
        INamedTypeSymbol modelSymbol, bool includeInherited)
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
                if (!seenNames.Add(prop.Name)) continue;
                yield return prop;
            }
            if (!includeInherited) break;
            current = current.BaseType;
        }
    }

    private static bool GetIncludeInherited(AttributeData attr)
    {
        foreach (var namedArg in attr.NamedArguments)
            if (namedArg.Key == "IncludeInherited" && namedArg.Value.Value is bool val)
                return val;
        return false;
    }

    private static HashSet<string> GetFlattenProperties(AttributeData attr)
    {
        var result = new HashSet<string>();
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key != "Flatten") continue;
            foreach (var item in namedArg.Value.Values)
                if (item.Value is string name)
                    result.Add(name);
        }
        return result;
    }

    private static FlattenPrefixMode GetFlattenPrefix(AttributeData attr)
    {
        foreach (var namedArg in attr.NamedArguments)
            if (namedArg.Key == "FlattenPrefix" && namedArg.Value.Value is int val)
                return (FlattenPrefixMode)val;
        return FlattenPrefixMode.Parent;
    }

    private static RepositoryKind GetRepositoryKind(AttributeData attr)
    {
        foreach (var namedArg in attr.NamedArguments)
            if (namedArg.Key == "Repository" && namedArg.Value.Value is int val)
                return (RepositoryKind)val;
        return RepositoryKind.None;
    }

    private static Dictionary<string, string> GetRenameMap(INamedTypeSymbol classSymbol)
    {
        var map = new Dictionary<string, string>();
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

    private static bool TryGetFromModelAttributeData(INamedTypeSymbol classSymbol, [NotNullWhen(true)] out AttributeData? attr)
    {
        attr = null;
        foreach (var a in classSymbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == DefaultSource.FromModelAttribute.Name)
            {
                attr = a;
                break;
            }
        }

        return attr is not null && attr.ConstructorArguments.Length > 0;
    }

    private static bool TryGetModelSymbol(AttributeData attr, [NotNullWhen(true)] out INamedTypeSymbol? modelSymbol)
    {
        var arg = attr.ConstructorArguments[0];
        modelSymbol = null;

        if (arg.Kind != TypedConstantKind.Type) return false;
        if (arg.Value is not INamedTypeSymbol ms) return false;

        modelSymbol = ms;
        return true;
    }

    private static string? GetInitializer(IPropertySymbol prop)
    {
        if (prop.DeclaringSyntaxReferences.Length > 0
            && prop.DeclaringSyntaxReferences[0].GetSyntax() is PropertyDeclarationSyntax propSyntax
            && propSyntax.Initializer is not null)
            return propSyntax.Initializer.Value.ToString();

        return null;
    }
}
