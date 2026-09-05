namespace Gener8.Contexts;

/// <summary>
/// PropertyTypeData represents the type information of a property.
/// </summary>
/// <param name="Type">The type of the property</param>
/// <param name="HasTypeMapping">True when the property type or its supported generic argument was remapped via TypeMappingAttribute</param>
/// <param name="HasGenericTypeMapping">True when a supported generic argument (e.g. List&lt;T&gt;'s T) was remapped via TypeMappingAttribute</param>
/// <param name="NeedsSpreadAssignment">True when model type is abstract collection (e.g. IReadOnlyCollection&lt;T&gt;) remapped to List&lt;T&gt;, or abstract dictionary interface remapped to Dictionary&lt;K,V&gt;</param>
/// <param name="IsEnum">True when the property type is an enum</param>
/// <param name="IsNullable">True when the property type is null-able</param>
/// <param name="EnumCollectionElementType">Non-null when the property is a collection of enums; holds the element type display (e.g. "CategoryEnum" or "CategoryEnum?")</param>
/// <param name="IsDictionary">True when the property type is a known two-argument dictionary</param>
/// <param name="DictionaryKeyToDtoMethodName">ToDto method name for the key type (null when key is not remapped)</param>
internal sealed record PropertyTypeData(
    string Type,
    bool HasTypeMapping,
    bool HasGenericTypeMapping,
    bool NeedsSpreadAssignment,
    bool IsEnum,
    bool IsNullable,
    string? EnumCollectionElementType = null,
    bool IsNullableValueType = false,
    string? ToModelCastType = null,
    string? TypeMappedToDtoMethodName = null,
    bool IsDictionary = false,
    string? DictionaryKeyToDtoMethodName = null
    );
