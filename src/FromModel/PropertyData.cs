namespace FromModel;

internal sealed record PropertyData(
    string Type,
    string Name,
    bool HasGetter,
    bool HasSetter,
    bool IsInitOnly,
    bool IsRequired,
    string? Initializer
);
