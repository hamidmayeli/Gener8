using Gener8.Contexts;
using System.Collections.Generic;

namespace Gener8.ContextBuilders;

internal sealed record PropertyBuildResult(
    IReadOnlyCollection<PropertyData> Properties,
    IReadOnlyCollection<AutoDtoTarget> AutoTargets,
    IReadOnlyList<BuildDiagnostic> Diagnostics);
