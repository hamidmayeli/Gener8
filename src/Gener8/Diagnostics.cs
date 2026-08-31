using Microsoft.CodeAnalysis;

namespace Gener8;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor UnresolvedModelType = new(
        id: "GEN001",
        title: "Unresolved model type",
        messageFormat: "Cannot generate DTO for '{0}': the model type could not be resolved. Verify the type is accessible and the typeof() argument is correct.",
        category: "Gener8",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AlreadyNullableProperty = new(
        id: "GEN002",
        title: "Property is already nullable",
        messageFormat: "ForceNullable: property '{0}' on model '{1}' is already nullable. Remove it from ForceNullable or make the model property non-nullable.",
        category: "Gener8",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ISetPropertyWithInitializer = new(
        id: "GEN003",
        title: "ISet<T> property has initializer",
        messageFormat: "Property '{0}' on model '{1}' is of type ISet<T> and has a property initializer. Initializers are copied verbatim and may not compile in the generated DTO. Remove the initializer and set the default value in a constructor instead.",
        category: "Gener8",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnexpectedError = new(
        id: "GEN999",
        title: "Unexpected generator error",
        messageFormat: "Gener8 failed while processing '{0}': {1}",
        category: "Gener8",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
