namespace Gener8.Contexts;

/// <summary>
/// Represents a model class with its fully qualified name and simple name.
/// </summary>
/// <param name="FullName">global::-prefixed fully qualified model type name</param>
/// <param name="Name">The name of the model type</param>
internal sealed record ModelClass(
    string FullName,
    string Name
    );
