using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Gener8.ContextBuilders;

// Builds the DTO property list for one model type.
//
// This type only orchestrates: options come from FromModelOptions, type mapping from
// TypeMappingResolver, type shape from PropertyTypeResolver, property enumeration from
// ModelPropertyReader, and constructor matching from ConstructorMatcher. Each instance
// builds exactly once, so no result can be accumulated twice.
internal sealed class PropertyDataBuilder
{
    private readonly PropertyBuildRequest _request;
    private readonly TypeMappingResolver _mappings;
    private readonly PropertyTypeResolver _types;
    private readonly List<BuildDiagnostic> _diagnostics = [];

    private PropertyDataBuilder(PropertyBuildRequest request)
    {
        _request = request;
        _mappings = new(
            request.ClassSymbol,
            request.ModelSymbol,
            request.QualifyingNamespaces,
            request.IgnoredTypeMappings,
            request.DtoSuffix);
        _types = new(_mappings, RepositoryProfile.For(request.Repository));
    }

    public static PropertyBuildResult Build(PropertyBuildRequest request)
        => new PropertyDataBuilder(request).Build();

    private PropertyBuildResult Build()
    {
        var options = FromModelOptions.Read(_request.Attribute, _request.OnlyIncludePaths);

        if (options.OnlyInclude.IsActive && options.Ignored.Count > 0)
            Report(Diagnostics.OnlyIncludeIgnoreConflict, _request.DtoName);

        var constructorBacked = ConstructorMatcher.MatchToLookup(_request.ModelSymbol);

        _mappings.AddInferredMappings(options.IncludeInherited, options.OnlyInclude, constructorBacked.Keys);

        var renames = ReadRenames();
        var userDeclaredNames = ReadUserDeclaredPropertyNames();

        var properties = new List<PropertyData>();

        foreach (var property in ModelPropertyReader.ReadSettable(
            _request.ModelSymbol, options.IncludeInherited, constructorBacked.Keys))
        {
            if (options.Ignored.Contains(property.Name)) continue;
            if (!options.OnlyInclude.Includes(property.Name)) continue;

            if (options.Flattened.Contains(property.Name))
                AddFlattenedProperties(properties, property, options, userDeclaredNames);
            else
                AddProperty(properties, property, options, renames, userDeclaredNames, constructorBacked);
        }

        ReportInvalidOnlyIncludePaths(options);

        return new(properties, _mappings.AutoTargets, _diagnostics);
    }

    // Inlines the nested type's properties, each remembering the parent it came from so ToModel
    // can reconstruct it.
    private void AddFlattenedProperties(
        List<PropertyData> properties,
        IPropertySymbol property,
        FromModelOptions options,
        HashSet<string> userDeclaredNames)
    {
        if (property.Type is not INamedTypeSymbol nestedType) return;

        var parent = new FlattenParent(
            property.Name,
            nestedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            property.NullableAnnotation == NullableAnnotation.Annotated);

        foreach (var nested in ModelPropertyReader.ReadSettable(nestedType, includeInherited: false))
        {
            var name = options.FlattenedName(property.Name, nested.Name);
            properties.Add(BuildProperty(nested, name, userDeclaredNames.Contains(name), parent));
        }
    }

    private void AddProperty(
        List<PropertyData> properties,
        IPropertySymbol property,
        FromModelOptions options,
        Dictionary<string, string> renames,
        HashSet<string> userDeclaredNames,
        Dictionary<string, string?> constructorBacked)
    {
        var isForceNullable = options.ForceNullable.Contains(property.Name);
        if (isForceNullable && property.NullableAnnotation == NullableAnnotation.Annotated)
        {
            Report(Diagnostics.AlreadyNullableProperty, property.Name, _request.ModelSymbol.Name);
            isForceNullable = false;
        }

        ReportUncopyableSetInitializer(property);
        ReportDynamoDbNonStringDictionaryKey(property);

        var name = renames.TryGetValue(property.Name, out var renamed) ? renamed : property.Name;
        constructorBacked.TryGetValue(property.Name, out var constructorDefault);

        properties.Add(
            BuildProperty(
                property,
                name,
                userDeclaredNames.Contains(name),
                parent: null,
                isConstructorBacked: constructorBacked.ContainsKey(property.Name),
                isForceNullable: isForceNullable,
                constructorDefault: constructorDefault));
    }

    private PropertyData BuildProperty(
        IPropertySymbol property,
        string name,
        bool isUserDeclared,
        FlattenParent? parent,
        bool isConstructorBacked = false,
        bool isForceNullable = false,
        string? constructorDefault = null)
    {
        var isParentNullable = parent?.IsNullable ?? false;

        // A get-only model property backed by a constructor needs init in the DTO so it can be populated.
        var isInitOnly = property.SetMethod is { IsInitOnly: true }
            || (property.SetMethod is null && isConstructorBacked);

        // When the model comes from a compiled (referenced) assembly, DeclaringSyntaxReferences is
        // empty and GetInitializer will return null — not because there is no initializer, but
        // because we cannot see the source. For non-nullable reference types this would produce a
        // CS8618 on the generated DTO; mark the property so SourceProducer can emit 'required'.
        var isInitializerUnknown = property.DeclaringSyntaxReferences.Length == 0
            && property.Type.IsReferenceType
            && property.NullableAnnotation == Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated
            && !property.IsRequired;

        return new PropertyData(
            _types.Resolve(property, isParentNullable, isForceNullable),
            name,
            property.GetMethod is not null,
            property.SetMethod is not null && !property.SetMethod.IsInitOnly && !isInitOnly,
            isInitOnly,
            // ForceNullable suppresses required: a nullable DTO property cannot be required in a
            // meaningful way. Neither can one reached through a nullable flatten parent.
            property.IsRequired && !isParentNullable && !isForceNullable,
            GetInitializer(property) ?? constructorDefault,
            // The original model property name, needed only when the DTO renamed it.
            parent is null && name != property.Name ? property.Name : null,
            isUserDeclared,
            BuildFlattenedData(property, parent),
            isForceNullable,
            // ForceNullable: capture the globally-qualified model type for the partial method stub.
            isForceNullable ? property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null,
            isInitializerUnknown);
    }

    private static FlattenedPropertyData? BuildFlattenedData(IPropertySymbol property, FlattenParent? parent)
    {
        if (parent is null) return null;

        var (parentName, parentTypeFullName, parentIsNullable) = parent.Value;

        return new(
            parentIsNullable ? $"{parentName}?.{property.Name}" : $"{parentName}.{property.Name}",
            parentName,
            parentTypeFullName,
            property.Name,
            property.Type.ToDisplayString().EndsWith("?"));
    }

    private void ReportInvalidOnlyIncludePaths(FromModelOptions options)
    {
        if (!options.OnlyInclude.IsActive) return;

        var modelPropertyNames = ModelPropertyReader.ReadNames(_request.ModelSymbol, options.IncludeInherited);

        foreach (var name in options.OnlyInclude.RootNames)
            if (!modelPropertyNames.Contains(name))
                Report(Diagnostics.InvalidOnlyIncludePath, name, _request.ModelSymbol.Name);
    }

    private void ReportDynamoDbNonStringDictionaryKey(IPropertySymbol property)
    {
        if (_request.Repository != RepositoryKind.DynamoDb) return;
        if (property.Type is not INamedTypeSymbol { IsGenericType: true, Arity: 2 } namedType) return;
        if (!KnownCollections.IsKnownDictionary(namedType)) return;
        if (namedType.TypeArguments[0].SpecialType == SpecialType.System_String) return;

        Report(Diagnostics.DynamoDbDictionaryNonStringKey, property.Name, _request.ModelSymbol.Name);
    }

    // Initializers are copied verbatim to the DTO. Collection-expression initializers (= [])
    // adapt to any target type and are safe; a typed one (= new HashSet<T>()) may not compile
    // against the remapped DTO property type.
    private void ReportUncopyableSetInitializer(IPropertySymbol property)
    {
        if (!KnownCollections.IsSet(property.Type)) return;
        if (GetInitializer(property) is not { } initializer) return;
        if (initializer.StartsWith("[", System.StringComparison.Ordinal)) return;

        Report(Diagnostics.ISetPropertyWithInitializer, property.Name, _request.ModelSymbol.Name);
    }

    private Dictionary<string, string> ReadRenames()
    {
        var renames = new Dictionary<string, string>();

        foreach (var (source, target) in AttributeReader.GetStringPairs(
            _request.ClassSymbol, DefaultSource.RenamePropertyAttribute.Name))
            renames[source] = target;

        return renames;
    }

    // Properties the user already wrote on the partial DTO: skipped when emitting, but still
    // mapped in ToModel/ToDto.
    private HashSet<string> ReadUserDeclaredPropertyNames()
    {
        var names = new HashSet<string>();
        if (_request.ClassSymbol is null) return names;

        foreach (var member in _request.ClassSymbol.GetMembers())
            if (member is IPropertySymbol { IsStatic: false } property)
                names.Add(property.Name);

        return names;
    }

    private static string? GetInitializer(IPropertySymbol property)
    {
        if (property.DeclaringSyntaxReferences.Length > 0
            && property.DeclaringSyntaxReferences[0].GetSyntax() is PropertyDeclarationSyntax { Initializer: not null } syntax)
            return syntax.Initializer.Value.ToString();

        return null;
    }

    private void Report(DiagnosticDescriptor descriptor, params object?[] messageArgs)
        => _diagnostics.Add(new(descriptor, messageArgs));
}
