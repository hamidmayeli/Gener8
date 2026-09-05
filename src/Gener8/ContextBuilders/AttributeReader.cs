using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace Gener8.ContextBuilders;

// Reads values out of attribute data. Every named argument and constructor argument the
// generator understands is extracted through here, so the null-checking and TypedConstant
// unwrapping exists once rather than once per option.
internal static class AttributeReader
{
    // Named argument holding a string array, in declaration order (e.g. Ignore = ["A", "B"]).
    public static List<string> GetStringList(AttributeData? attribute, string key)
    {
        var result = new List<string>();
        if (attribute is null) return result;

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key != key) continue;

            foreach (var item in namedArg.Value.Values)
                if (item.Value is string value)
                    result.Add(value);
        }

        return result;
    }

    public static HashSet<string> GetStringSet(AttributeData? attribute, string key)
        => [.. GetStringList(attribute, key)];

    public static bool GetBool(AttributeData? attribute, string key, bool defaultValue = false)
    {
        if (attribute is null) return defaultValue;

        foreach (var namedArg in attribute.NamedArguments)
            if (namedArg.Key == key && namedArg.Value.Value is bool value)
                return value;

        return defaultValue;
    }

    // Enum-typed named arguments arrive as boxed ints in the TypedConstant.
    public static TEnum GetEnum<TEnum>(AttributeData? attribute, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (attribute is null) return defaultValue;

        foreach (var namedArg in attribute.NamedArguments)
            if (namedArg.Key == key && namedArg.Value.Value is int value)
                return (TEnum)(object)value;

        return defaultValue;
    }

    // Class-level attributes of the given fully-qualified name carrying (Type, Type) arguments.
    public static IEnumerable<(INamedTypeSymbol Source, INamedTypeSymbol Target)> GetTypePairs(
        INamedTypeSymbol? classSymbol, string attributeName)
    {
        foreach (var attribute in GetAttributes(classSymbol, attributeName, argumentCount: 2))
        {
            if (attribute.ConstructorArguments[0].Kind != TypedConstantKind.Type) continue;
            if (attribute.ConstructorArguments[1].Kind != TypedConstantKind.Type) continue;
            if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol source) continue;
            if (attribute.ConstructorArguments[1].Value is not INamedTypeSymbol target) continue;

            yield return (source, target);
        }
    }

    // Class-level attributes of the given fully-qualified name carrying (string, string) arguments.
    public static IEnumerable<(string Source, string Target)> GetStringPairs(
        INamedTypeSymbol? classSymbol, string attributeName)
    {
        foreach (var attribute in GetAttributes(classSymbol, attributeName, argumentCount: 2))
        {
            if (attribute.ConstructorArguments[0].Value is not string source) continue;
            if (attribute.ConstructorArguments[1].Value is not string target) continue;

            yield return (source, target);
        }
    }

    // Class-level attributes of the given fully-qualified name carrying a single Type argument.
    public static IEnumerable<INamedTypeSymbol> GetSingleTypeArguments(
        INamedTypeSymbol? classSymbol, string attributeName)
    {
        foreach (var attribute in GetAttributes(classSymbol, attributeName, argumentCount: 1))
            if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol type)
                yield return type;
    }

    public static AttributeData? FindAttribute(INamedTypeSymbol? classSymbol, string attributeName)
    {
        foreach (var attribute in GetAttributes(classSymbol, attributeName, argumentCount: 0))
            return attribute;

        return null;
    }

    private static IEnumerable<AttributeData> GetAttributes(
        INamedTypeSymbol? classSymbol, string attributeName, int argumentCount)
    {
        if (classSymbol is null) yield break;

        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != attributeName) continue;
            if (attribute.ConstructorArguments.Length < argumentCount) continue;

            yield return attribute;
        }
    }
}
