using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
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

        var properties = new PropertyDataBuilder(classSymbol, attr, modelSymbol, repositoryKind).GetProperties();

        return new TargetClass(
            classSymbol.Name,
            ns,
            accessibility,
            properties,
            new(modelFullName, modelSymbol.Name),
            repositoryKind);
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
