namespace Gener8.Contexts;

internal sealed record ModelClass(
    /// global::-prefixed fully qualified model type name
    string FullName,
    string Name
    );
