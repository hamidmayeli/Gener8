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

    public static TargetClass? ExtractClassTarget(GeneratorSyntaxContext context)
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

        var repositoryKind = GetRepositoryKind(attr);
        var modelFullName = modelSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var qualifyingNamespaces = GetQualifyingNamespaces(attr, modelSymbol);

        var builder = new PropertyDataBuilder(classSymbol, attr, modelSymbol, repositoryKind, qualifyingNamespaces);
        var properties = builder.GetProperties();

        var autoTargets = BuildAutoTargets(builder.AutoTargetSymbols, ns, accessibility, qualifyingNamespaces, repositoryKind);

        return new TargetClass(
            classSymbol.Name,
            ns,
            accessibility,
            properties,
            new(modelFullName, modelSymbol.Name),
            repositoryKind,
            autoTargets);
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

        // DtoNamespaces from attribute
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key != "DtoNamespaces") continue;
            foreach (var item in namedArg.Value.Values)
                if (item.Value is string extraNs)
                    namespaces.Add(extraNs);
        }

        return namespaces;
    }

    // Recursively synthesises TargetClass records for all transitive auto-DTO types.
    // Returns a flat list (depth-first) safe to iterate and de-duplicate in the pipeline.
    private static IReadOnlyCollection<TargetClass> BuildAutoTargets(
        IReadOnlyCollection<INamedTypeSymbol> symbols,
        string? targetNs,
        string accessibility,
        IReadOnlyCollection<string> qualifyingNamespaces,
        RepositoryKind repositoryKind)
    {
        var result = new List<TargetClass>();
        var visited = new HashSet<string>();
        CollectAutoTargets(symbols, targetNs, accessibility, qualifyingNamespaces, repositoryKind, visited, result);
        return result;
    }

    private static void CollectAutoTargets(
        IReadOnlyCollection<INamedTypeSymbol> symbols,
        string? targetNs,
        string accessibility,
        IReadOnlyCollection<string> qualifyingNamespaces,
        RepositoryKind repositoryKind,
        HashSet<string> visited,
        List<TargetClass> result)
    {
        foreach (var symbol in symbols)
        {
            var key = symbol.ToDisplayString();
            if (!visited.Add(key)) continue;

            var dtoName = symbol.Name + "Dto";
            var modelFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // No classSymbol/attribute for synthesised DTOs — all options default to empty/false.
            // Propagate repositoryKind so that e.g. DynamoDB enum converter attributes are emitted.
            var builder = new PropertyDataBuilder(null, null, symbol, repositoryKind, qualifyingNamespaces);
            var props = builder.GetProperties();

            // Depth-first: add nested auto-targets before this one so dependencies come first.
            CollectAutoTargets(builder.AutoTargetSymbols, targetNs, accessibility, qualifyingNamespaces, repositoryKind, visited, result);

            result.Add(new TargetClass(
                dtoName,
                targetNs,
                accessibility,
                props,
                new ModelClass(modelFullName, symbol.Name),
                repositoryKind,
                []));
        }
    }

    private static RepositoryKind GetRepositoryKind(AttributeData attr)
    {
        foreach (var namedArg in attr.NamedArguments)
            if (namedArg.Key == "Repository" && namedArg.Value.Value is int val)
                return (RepositoryKind)val;

        return RepositoryKind.None;
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
}
