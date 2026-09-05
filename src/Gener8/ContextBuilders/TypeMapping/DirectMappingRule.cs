using Gener8.Contexts;

namespace Gener8.ContextBuilders.TypeMapping;

// The property's own type is remapped: Customer -> CustomerDto.
internal sealed class DirectMappingRule : ITypeMappingRule
{
    public bool TryApply(in TypeMappingContext context, out MappedType mapped)
    {
        if (!context.Mappings.TryGetMapping(context.OriginalTypeDisplay, out var mappedType))
        {
            mapped = default;
            return false;
        }

        mapped = new(
            TypeNames.WithNullable(mappedType, context.IsNullable),
            IsGeneric: false,
            ElementType: null,
            TypeNames.DtoMethodName(context.OriginalTypeDisplay, mappedType));

        return true;
    }
}
