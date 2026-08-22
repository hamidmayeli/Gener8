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

    public static readonly DiagnosticDescriptor UnexpectedError = new(
        id: "GEN999",
        title: "Unexpected generator error",
        messageFormat: "Gener8 failed while processing '{0}': {1}",
        category: "Gener8",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
