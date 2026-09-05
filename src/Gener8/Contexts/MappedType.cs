namespace Gener8.Contexts;

// The outcome of applying a type-mapping rule to a model property type.
internal readonly record struct MappedType(
    string TypeDisplay,          // DTO-side display string, nullability already applied
    bool IsGeneric,              // true when a type argument was remapped rather than the type itself
    string? ElementType,         // mapped element type display for collection mappings
    string? ToDtoMethodName);    // the mapping method to call on the model-side value
