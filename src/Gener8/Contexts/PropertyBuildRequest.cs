using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.Contexts;

// Everything the property builder needs, in one of two shapes.
//
// A *declared* DTO is a partial class the user decorated with [FromModel]: it has a class
// symbol (for [TypeMapping]/[RenameProperty]/user-declared properties) and the attribute
// itself (for Ignore/Flatten/ForceNullable/...).
//
// An *auto* DTO is synthesised for a nested model type discovered through DtoNamespaces:
// it has neither, and instead inherits the OnlyInclude sub-paths of the property that
// introduced it.
internal sealed record PropertyBuildRequest(
    INamedTypeSymbol ModelSymbol,
    string DtoName,
    RepositoryKind Repository,
    IReadOnlyCollection<string>? QualifyingNamespaces,
    IReadOnlyCollection<string>? IgnoredTypeMappings,
    string DtoSuffix,
    INamedTypeSymbol? ClassSymbol = null,
    AttributeData? Attribute = null,
    IReadOnlyCollection<string>? OnlyIncludePaths = null)
{
    public static PropertyBuildRequest ForDeclaredDto(
        INamedTypeSymbol classSymbol,
        AttributeData attribute,
        INamedTypeSymbol modelSymbol,
        RepositoryKind repository,
        IReadOnlyCollection<string> qualifyingNamespaces,
        IReadOnlyCollection<string> ignoredTypeMappings,
        string dtoSuffix)
        => new(
            modelSymbol,
            classSymbol.Name,
            repository,
            qualifyingNamespaces,
            ignoredTypeMappings,
            dtoSuffix,
            ClassSymbol: classSymbol,
            Attribute: attribute);

    public static PropertyBuildRequest ForAutoDto(
        INamedTypeSymbol modelSymbol,
        string dtoName,
        RepositoryKind repository,
        IReadOnlyCollection<string> qualifyingNamespaces,
        IReadOnlyCollection<string> ignoredTypeMappings,
        string dtoSuffix,
        IReadOnlyCollection<string>? onlyIncludePaths)
        => new(
            modelSymbol,
            dtoName,
            repository,
            qualifyingNamespaces,
            ignoredTypeMappings,
            dtoSuffix,
            OnlyIncludePaths: onlyIncludePaths);
}
