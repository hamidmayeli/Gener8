using System.Collections.Immutable;

namespace Gener8.Contexts;

/// <summary>
/// Represents a model class with its fully qualified name and simple name.
/// </summary>
/// <param name="FullName">global::-prefixed fully qualified model type name</param>
/// <param name="Name">The name of the model type</param>
/// <param name="PrimaryConstructorParams">Ordered constructor parameter names when the model uses a primary constructor (e.g. positional records); default when object-initializer style should be used</param>
internal sealed record ModelClass(
    string FullName,
    string Name,
    ImmutableArray<string> PrimaryConstructorParams
    );
