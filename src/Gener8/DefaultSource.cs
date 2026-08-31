// Attribute and enum type definitions have moved to Gener8.Abstractions.
// This file retains only the fully-qualified type name constants used by the generator's
// Roslyn attribute-matching logic (INamedTypeSymbol.ToDisplayString() comparisons).
namespace Gener8;

internal static class DefaultSource
{
    public static class FromModelAttribute          { public const string Name = "Gener8.FromModelAttribute"; }
    public static class TypeMappingAttribute        { public const string Name = "Gener8.TypeMappingAttribute"; }
    public static class RenamePropertyAttribute     { public const string Name = "Gener8.RenamePropertyAttribute"; }
    public static class IgnoreTypeMappingAttribute  { public const string Name = "Gener8.IgnoreTypeMappingAttribute"; }
}
