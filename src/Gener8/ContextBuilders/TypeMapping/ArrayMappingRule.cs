using Gener8.Contexts;
using Microsoft.CodeAnalysis;

namespace Gener8.ContextBuilders.TypeMapping;

// An array's element type is remapped, and the DTO uses a list: Product[] -> List<ProductDto>.
internal sealed class ArrayMappingRule : ITypeMappingRule
{
    public bool TryApply(in TypeMappingContext context, out MappedType mapped)
    {
        if (context.Type is not IArrayTypeSymbol { ElementType: var elementType }
            || !context.Mappings.TryGetElementMapping(elementType, out var mappedElementType))
        {
            mapped = default;
            return false;
        }

        mapped = new(
            TypeNames.WithNullable(KnownCollections.List(mappedElementType), context.IsNullable),
            IsGeneric: true,
            mappedElementType,
            TypeNames.DtoMethodName(elementType.ToDisplayString(), mappedElementType));

        return true;
    }
}
