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
    bool IsUserDeclared,        // true when the DTO already declares this property; skip in EmitModel, keep in mappings
    bool NeedsSpreadAssignment, // true when model type is abstract collection (e.g. IReadOnlyCollection<T>) and DTO uses List<T>
    string? FlattenedParentName,            // parent property name for flatten reconstruction (e.g. "Category")
    string? FlattenedParentTypeFullName,    // fully-qualified parent type for 'new ParentType { }' in ToModel
    string? FlattenedNestedPropertyName,    // the property name on the nested type (e.g. "Name"), used in reconstruction
    bool FlattenedOriginallyNullable        // true when the nested property type was nullable before parent nullability was applied
);
