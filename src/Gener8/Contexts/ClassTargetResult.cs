using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.Contexts;

internal sealed record ClassTargetResult(TargetClass? Target, IReadOnlyList<Diagnostic>? Errors = null);
