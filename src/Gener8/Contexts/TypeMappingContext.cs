using Gener8.ContextBuilders;
using Microsoft.CodeAnalysis;

namespace Gener8.Contexts;

internal readonly record struct TypeMappingContext(
    ITypeSymbol Type,
    string OriginalTypeDisplay,
    bool IsNullable,
    TypeMappingResolver Mappings);
