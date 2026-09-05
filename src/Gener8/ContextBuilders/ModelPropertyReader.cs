using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.ContextBuilders;

// The single base-type walk over a model's public instance properties.
// Walking stops at System.Object and de-duplicates by name so an override or `new` member
// on a derived type shadows the inherited one.
internal static class ModelPropertyReader
{
    private static IEnumerable<IPropertySymbol> Read(INamedTypeSymbol modelSymbol, bool includeInherited)
        => Read(modelSymbol, includeInherited, settableOnly: false, constructorBackedNames: null);

    // Restricted to properties the DTO can populate: those with a set/init accessor, plus
    // get-only properties backed by a matching constructor parameter.
    public static IEnumerable<IPropertySymbol> ReadSettable(
        INamedTypeSymbol modelSymbol,
        bool includeInherited,
        ICollection<string>? constructorBackedNames = null)
        => Read(modelSymbol, includeInherited, settableOnly: true, constructorBackedNames);

    public static HashSet<string> ReadNames(INamedTypeSymbol modelSymbol, bool includeInherited)
    {
        var names = new HashSet<string>();

        foreach (var property in Read(modelSymbol, includeInherited))
            names.Add(property.Name);

        return names;
    }

    private static IEnumerable<IPropertySymbol> Read(
        INamedTypeSymbol modelSymbol,
        bool includeInherited,
        bool settableOnly,
        ICollection<string>? constructorBackedNames)
    {
        var seenNames = new HashSet<string>();
        var current = modelSymbol;

        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } property)
                    continue;

                if (settableOnly
                    && property.SetMethod is null
                    && !(constructorBackedNames?.Contains(property.Name) ?? false))
                    continue;

                if (!seenNames.Add(property.Name)) continue;

                yield return property;
            }

            if (!includeInherited) break;

            current = current.BaseType;
        }
    }
}
