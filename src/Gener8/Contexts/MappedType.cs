namespace Gener8.Contexts;

// The outcome of applying a type-mapping rule to a model property type.
internal readonly record struct MappedType(
    string TypeDisplay,                    // DTO-side display string, nullability already applied
    bool IsGeneric,                        // true when a type argument was remapped rather than the type itself
    string? ElementType,                   // mapped value type for collections/dicts; null when not remapped
    string? ToDtoMethodName,               // ToDto method for value type (collections) or value type (dicts)
    bool IsDictionary = false,             // true when the matched type is a known dictionary
    string? DictionaryMappedKeyType = null,     // remapped key type display (null when key not remapped)
    string? DictionaryKeyToDtoMethodName = null); // ToDto method for key type (null when key not remapped)
