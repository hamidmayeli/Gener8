using Gener8.ContextBuilders;
using Gener8.Contexts;
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

    public static ClassTargetResult? ExtractClassTarget(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol classSymbol)
            return null;

        if (!TryGetFromModelAttributeData(classSymbol, out AttributeData? attr)) return null;

        var location = context.Node.GetLocation();

        if (!TryGetModelSymbol(attr, out INamedTypeSymbol? modelSymbol))
        {
            var diagnostic = Diagnostic.Create(
                Diagnostics.UnresolvedModelType,
                location,
                classSymbol.Name);
            return new ClassTargetResult(null, [diagnostic]);
        }

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

        var repositoryKind = AttributeReader.GetEnum(attr, "Repository", RepositoryKind.None);
        var modelFullName = modelSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var qualifyingNamespaces = GetQualifyingNamespaces(attr, modelSymbol);
        var ignoredTypeMappings = GetIgnoredTypeMappings(classSymbol);
        var dtoSuffix = TypeNames.DtoSuffix(modelSymbol.Name, classSymbol.Name);

        var built = PropertyDataBuilder.Build(
            PropertyBuildRequest.ForDeclaredDto(
                classSymbol, attr, modelSymbol, repositoryKind, qualifyingNamespaces, ignoredTypeMappings, dtoSuffix));

        var errors = new List<Diagnostic>();
        foreach (var diagnostic in built.Diagnostics)
            errors.Add(diagnostic.ToDiagnostic(location));

        var autoTargets = BuildAutoTargets(
            built.AutoTargets, ns, accessibility, qualifyingNamespaces, repositoryKind,
            ignoredTypeMappings, dtoSuffix, location, errors);

        // Any error means the DTO cannot be emitted safely; report and drop the target.
        if (errors.Count > 0) return new ClassTargetResult(null, errors);

        var target = new TargetClass(
            classSymbol.Name,
            ns,
            accessibility,
            built.Properties,
            new(modelFullName, modelSymbol.Name, ConstructorMatcher.MatchToNames(modelSymbol)),
            repositoryKind,
            autoTargets,
            TypeNames.DtoMethodName(modelSymbol.Name, classSymbol.Name));

        return new ClassTargetResult(target, null);
    }

    private static IReadOnlyCollection<string> GetQualifyingNamespaces(
        AttributeData attr,
        INamedTypeSymbol modelSymbol)
    {
        var namespaces = new HashSet<string>();

        // Default: model's own namespace (empty string = global namespace)
        var modelNs = modelSymbol.ContainingNamespace is { IsGlobalNamespace: false } modelNsSymbol
            ? modelNsSymbol.ToDisplayString()
            : "";
        namespaces.Add(modelNs);

        foreach (var extraNs in AttributeReader.GetStringList(attr, "DtoNamespaces"))
            namespaces.Add(extraNs);

        return namespaces;
    }

    // Recursively synthesises TargetClass records for all transitive auto-DTO types.
    // Returns a flat list (depth-first) safe to iterate and de-duplicate in the pipeline.
    private static IReadOnlyCollection<TargetClass> BuildAutoTargets(
        IReadOnlyCollection<AutoDtoTarget> autoTargets,
        string? targetNs,
        string accessibility,
        IReadOnlyCollection<string> qualifyingNamespaces,
        RepositoryKind repositoryKind,
        IReadOnlyCollection<string> ignoredTypeMappings,
        string dtoSuffix,
        Location? location,
        List<Diagnostic> errors)
    {
        var result = new List<TargetClass>();
        var visited = new HashSet<string>();
        CollectAutoTargets(
            autoTargets, targetNs, accessibility, qualifyingNamespaces, repositoryKind,
            ignoredTypeMappings, dtoSuffix, location, errors, visited, result);
        return result;
    }

    private static void CollectAutoTargets(
        IReadOnlyCollection<AutoDtoTarget> autoTargets,
        string? targetNs,
        string accessibility,
        IReadOnlyCollection<string> qualifyingNamespaces,
        RepositoryKind repositoryKind,
        IReadOnlyCollection<string> ignoredTypeMappings,
        string dtoSuffix,
        Location? location,
        List<Diagnostic> errors,
        HashSet<string> visited,
        List<TargetClass> result)
    {
        foreach (var (symbol, symbolOnlyIncludePaths) in autoTargets)
        {
            var key = symbol.ToDisplayString();
            if (!visited.Add(key)) continue;

            var dtoName = symbol.Name + dtoSuffix;
            var modelFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // A synthesised DTO has no class symbol or attribute, so all options default.
            // repositoryKind, ignoredTypeMappings, dtoSuffix and the OnlyInclude sub-paths are
            // propagated so child DTOs inherit the same suffix and property filter as the parent.
            var built = PropertyDataBuilder.Build(
                PropertyBuildRequest.ForAutoDto(
                    symbol, dtoName, repositoryKind, qualifyingNamespaces,
                    ignoredTypeMappings, dtoSuffix, symbolOnlyIncludePaths));

            // Diagnostics raised while building a child DTO (e.g. an OnlyInclude sub-path that
            // does not exist on the nested type) belong to the declaration that triggered it.
            foreach (var diagnostic in built.Diagnostics)
                errors.Add(diagnostic.ToDiagnostic(location));

            // Depth-first: add nested auto-targets before this one so dependencies come first.
            CollectAutoTargets(
                built.AutoTargets, targetNs, accessibility, qualifyingNamespaces, repositoryKind,
                ignoredTypeMappings, dtoSuffix, location, errors, visited, result);

            result.Add(new TargetClass(
                dtoName,
                targetNs,
                accessibility,
                built.Properties,
                new ModelClass(modelFullName, symbol.Name, ConstructorMatcher.MatchToNames(symbol)),
                repositoryKind,
                [],
                TypeNames.DtoMethodName(symbol.Name, dtoName)));
        }
    }

    private static IReadOnlyCollection<string> GetIgnoredTypeMappings(INamedTypeSymbol classSymbol)
    {
        var result = new HashSet<string>();

        foreach (var ignoredType in AttributeReader.GetSingleTypeArguments(
            classSymbol, DefaultSource.IgnoreTypeMappingAttribute.Name))
            result.Add(ignoredType.ToDisplayString());

        return result;
    }

    private static bool TryGetFromModelAttributeData(INamedTypeSymbol classSymbol, [NotNullWhen(true)] out AttributeData? attr)
    {
        attr = AttributeReader.FindAttribute(classSymbol, DefaultSource.FromModelAttribute.Name);

        return attr is not null && attr.ConstructorArguments.Length > 0;
    }

    private static bool TryGetModelSymbol(AttributeData attr, [NotNullWhen(true)] out INamedTypeSymbol? modelSymbol)
    {
        var arg = attr.ConstructorArguments[0];
        modelSymbol = null;

        if (arg.Kind != TypedConstantKind.Type) return false;
        if (arg.Value is not INamedTypeSymbol ms) return false;
        if (ms.TypeKind == TypeKind.Error) return false;

        modelSymbol = ms;
        return true;
    }
}
