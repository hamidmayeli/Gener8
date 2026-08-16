using System.Collections.Generic;

namespace Gener8.Contexts;

internal sealed record TargetClass(
    string ClassName,
    string? Namespace,
    string Accessibility,
    IReadOnlyCollection<PropertyData> Properties,
    ModelClass Model,
    RepositoryKind Repository
    );
