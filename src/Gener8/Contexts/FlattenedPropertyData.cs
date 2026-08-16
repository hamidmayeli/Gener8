namespace Gener8.Contexts;

internal sealed record FlattenedPropertyData(
    string ReadPath,  // model-side read expression for flattened properties (e.g. "Address?.Street")
    string ParentName,            // parent property name for flatten reconstruction (e.g. "Category")
    string ParentTypeFullName,    // fully-qualified parent type for 'new ParentType { }' in ToModel
    string NestedPropertyName,    // the property name on the nested type (e.g. "Name"), used in reconstruction
    bool OriginallyNullable        // true when the nested property type was nullable before parent nullability was applied
    );
