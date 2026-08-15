namespace Gener8;

internal sealed record PropertyData(
    string Type,
    string Name,
    bool HasGetter,
    bool HasSetter,
    bool IsInitOnly,
    bool IsRequired,
    string? Initializer,
    string? ModelPropertyName,  // original model property name when renamed; null = same as Name
    string? FlattenedReadPath,  // model-side read expression for flattened properties (e.g. "Address?.Street")
    bool HasTypeMapping,        // true when the property type was remapped via TypeMappingAttribute
    bool IsUserDeclared         // true when the DTO already declares this property; skip in EmitModel, keep in mappings
);
