using Gener8.Contexts;
using Microsoft.CodeAnalysis;

namespace Gener8.ContextBuilders.TypeMapping;

// A supported collection's element type is remapped: List<Customer> -> List<CustomerDto>.
internal sealed class CollectionMappingRule : ITypeMappingRule
{
    public bool TryApply(in TypeMappingContext context, out MappedType mapped)
    {
        if (!context.Mappings.TryGetCollectionMapping(context.Type, out var collection))
        {
            mapped = default;
            return false;
        }

        string? toDtoMethodName = null;
        if (context.Type is INamedTypeSymbol { IsGenericType: true, Arity: 1 } collectionType)
            toDtoMethodName = TypeNames.DtoMethodName(
                collectionType.TypeArguments[0].ToDisplayString(), collection.ElementTypeDisplay);

        mapped = new(
            TypeNames.WithNullable(collection.TypeDisplay, context.IsNullable),
            IsGeneric: true,
            collection.ElementTypeDisplay,
            toDtoMethodName);

        return true;
    }
}
