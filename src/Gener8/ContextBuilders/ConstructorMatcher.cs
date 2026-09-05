using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Gener8.ContextBuilders;

// Matches a model's constructor parameters to its public properties.
//
// Both the DTO property builder (which needs the parameter default values) and the mapping
// emitter (which needs the parameter order) depend on this decision. Keeping it in one place
// means the two cannot disagree — if they did, ToModel would emit a constructor call whose
// arguments the DTO never populated.
internal static class ConstructorMatcher
{
    // Returns the matched properties in constructor parameter order, or default when no
    // non-implicit constructor has parameters that all resolve to public properties
    // (i.e. object-initializer style should be used instead).
    //
    // Supports both records (PascalCase parameters) and regular classes (camelCase parameters
    // capitalized to match the property name).
    private static ImmutableArray<ConstructorBackedProperty> Match(INamedTypeSymbol modelSymbol)
    {
        var propertyNames = ModelPropertyReader.ReadNames(modelSymbol, includeInherited: false);
        if (propertyNames.Count == 0) return default;

        foreach (var constructor in modelSymbol.InstanceConstructors)
        {
            if (constructor.IsImplicitlyDeclared) continue;
            if (constructor.Parameters.Length == 0) continue;

            var matched = TryMatch(constructor, propertyNames);
            if (!matched.IsDefault) return matched;
        }

        return default;
    }

    // Convenience projection for callers that only need name → default value lookups.
    public static Dictionary<string, string?> MatchToLookup(INamedTypeSymbol modelSymbol)
    {
        var result = new Dictionary<string, string?>();
        var matched = Match(modelSymbol);

        if (!matched.IsDefault)
            foreach (var property in matched)
                result[property.Name] = property.DefaultValue;

        return result;
    }

    // Convenience projection for callers that only need the ordered parameter names.
    public static ImmutableArray<string> MatchToNames(INamedTypeSymbol modelSymbol)
    {
        var matched = Match(modelSymbol);
        if (matched.IsDefault) return default;

        var names = ImmutableArray.CreateBuilder<string>(matched.Length);
        foreach (var property in matched)
            names.Add(property.Name);

        return names.ToImmutable();
    }

    private static ImmutableArray<ConstructorBackedProperty> TryMatch(
        IMethodSymbol constructor, HashSet<string> propertyNames)
    {
        var matched = ImmutableArray.CreateBuilder<ConstructorBackedProperty>(constructor.Parameters.Length);

        foreach (var parameter in constructor.Parameters)
        {
            if (!TryResolvePropertyName(parameter.Name, propertyNames, out var propertyName))
                return default;

            matched.Add(new(propertyName, GetDefaultValue(parameter)));
        }

        return matched.ToImmutable();
    }

    // PascalCase exact match (records), then capitalize-first fallback (regular classes).
    private static bool TryResolvePropertyName(
        string parameterName, HashSet<string> propertyNames, out string propertyName)
    {
        if (propertyNames.Contains(parameterName))
        {
            propertyName = parameterName;
            return true;
        }

        propertyName = parameterName.Length > 0
            ? char.ToUpper(parameterName[0]) + parameterName.Substring(1)
            : parameterName;

        return propertyNames.Contains(propertyName);
    }

    private static string? GetDefaultValue(IParameterSymbol parameter)
    {
        foreach (var syntaxRef in parameter.DeclaringSyntaxReferences)
            if (syntaxRef.GetSyntax() is ParameterSyntax { Default: not null } parameterSyntax)
                return parameterSyntax.Default.Value.ToString();

        return null;
    }
}
