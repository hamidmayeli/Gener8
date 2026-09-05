using Microsoft.CodeAnalysis;

namespace Gener8;

// A diagnostic the property builder wants reported, deferred so the builder does not need to
// know the syntax location it will be anchored to.
internal readonly record struct BuildDiagnostic(DiagnosticDescriptor Descriptor, object?[] MessageArgs)
{
    public Diagnostic ToDiagnostic(Location? location) => Diagnostic.Create(Descriptor, location, MessageArgs);
}
