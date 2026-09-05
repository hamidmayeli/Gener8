using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.Contexts;

// A model type discovered during type-mapping inference that needs a DTO synthesised for it.
// OnlyIncludePaths carries the parent's OnlyInclude sub-paths down to the child DTO.
internal readonly record struct AutoDtoTarget(
    INamedTypeSymbol Symbol,
    IReadOnlyCollection<string>? OnlyIncludePaths);
