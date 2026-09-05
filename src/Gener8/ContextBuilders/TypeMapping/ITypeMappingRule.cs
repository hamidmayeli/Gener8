using Gener8.Contexts;

namespace Gener8.ContextBuilders.TypeMapping;

// Each rule recognises one shape of mapping. The resolver applies them in order and takes the
// first match, so the shapes stay mutually exclusive by construction rather than by an
// ever-growing chain of negated guards.
internal interface ITypeMappingRule
{
    bool TryApply(in TypeMappingContext context, out MappedType mapped);
}
