using System;

namespace Gener8;

// Shared helpers for manipulating type display strings and the DTO naming convention.
// Type identity throughout the generator is string-based (Roslyn display strings), so these
// are the single place where the '?' suffix and the model→DTO name convention are interpreted.
internal static class TypeNames
{
    public static string StripNullable(string type)
        => type.EndsWith("?") ? type.Substring(0, type.Length - 1) : type;

    private static string EnsureNullable(string type)
        => type.EndsWith("?") ? type : type + "?";

    public static string WithNullable(string type, bool isNullable)
        => isNullable ? EnsureNullable(type) : type;

    // Extracts the simple (unqualified) type name from a fully-qualified display string.
    public static string SimpleName(string displayName)
    {
        var lastDot = displayName.LastIndexOf('.');
        return lastDot >= 0 ? displayName.Substring(lastDot + 1) : displayName;
    }

    // When the DTO name starts with the model name as a prefix, the remaining suffix drives the
    // mapping method name: "Product" + "ProductView" → "ToView". Falls back to "ToDto" otherwise.
    // Accepts simple or fully-qualified names, with or without a trailing '?'.
    public static string DtoMethodName(string modelDisplay, string dtoDisplay)
    {
        var modelSimple = SimpleName(StripNullable(modelDisplay));
        var dtoSimple = SimpleName(StripNullable(dtoDisplay));

        if (dtoSimple.Length > modelSimple.Length && dtoSimple.StartsWith(modelSimple, StringComparison.Ordinal))
            return "To" + dtoSimple.Substring(modelSimple.Length);

        return "ToDto";
    }

    // The raw suffix appended to auto-generated child DTO names.
    // "Order"/"OrderView" → "View"; "Order"/"OrderDto" → "Dto"; no-match → "Dto".
    public static string DtoSuffix(string modelDisplay, string dtoDisplay)
        => DtoMethodName(modelDisplay, dtoDisplay).Substring(2);  // strip "To"
}
