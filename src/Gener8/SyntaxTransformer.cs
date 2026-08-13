using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Gener8;

internal static class SyntaxTransformer
{
    private enum FlattenPrefixMode { Parent = 0, None = 1, Gaped = 2 }

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

        var properties = new List<PropertyData>();
        foreach (var prop in GetModelProperties(modelSymbol, includeInherited))
        {
            if (ignoredNames.Contains(prop.Name)) continue;

            if (flattenNames.Contains(prop.Name))
            {
                if (prop.Type is INamedTypeSymbol nestedType)
                {
                    var parentIsNullable = prop.NullableAnnotation == NullableAnnotation.Annotated;
                    foreach (var nested in GetModelProperties(nestedType, includeInherited: false))
                    {
                        var nestedName = flattenPrefix switch
                        {
                            FlattenPrefixMode.Parent => prop.Name + nested.Name,
                            FlattenPrefixMode.Gaped => prop.Name + "_" + nested.Name,
                            _ => null
                        };
                        properties.Add(BuildPropertyData(nested, typeMappings, renameMap: null, nameOverride: nestedName, parentIsNullable: parentIsNullable, flattenParentName: prop.Name));
                    }
                }
                continue;
            }

            properties.Add(BuildPropertyData(prop, typeMappings, renameMap));
        }

        return new ClassTarget(classSymbol.Name, ns, accessibility, properties, modelFullName);
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
        string? nameOverride = null,
        bool parentIsNullable = false,
        string? flattenParentName = null)
    {
        var originalType = prop.Type.ToDisplayString();
        var hasTypeMapping = typeMappings.ContainsKey(originalType);
        var typeDisplay = hasTypeMapping ? typeMappings[originalType] : originalType;

        if (parentIsNullable && !typeDisplay.EndsWith("?"))
            typeDisplay += "?";

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
            hasTypeMapping);
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
