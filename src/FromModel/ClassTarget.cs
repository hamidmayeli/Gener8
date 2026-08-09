using System.Collections.Generic;

namespace FromModel;

internal sealed record ClassTarget(
    string ClassName,
    string? Namespace,
    string Accessibility,
    IReadOnlyCollection<PropertyData> Properties
    );
