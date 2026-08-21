namespace Gener8.Contexts;

/// <summary>
/// PropertyTypeData represents the type information of a property.
/// </summary>
/// <param name="Type">The type of the property</param>
/// <param name="HasTypeMapping">True when the property type or its supported generic argument was remapped via TypeMappingAttribute</param>
/// <param name="HasGenericTypeMapping">True when a supported generic argument (e.g. List<T>'s T) was remapped via TypeMappingAttribute</param>
/// <param name="NeedsSpreadAssignment">True when model type is abstract collection (e.g. IReadOnlyCollection<T>) and DTO uses List<T></param>
/// <param name="IsEnum">True when the property type is an enum</param>
/// <param name="IsNullable">True when the property type is null-able</param>
/// <param name="EnumCollectionElementType">Non-null when the property is a collection of enums; holds the element type display (e.g. "CategoryEnum" or "CategoryEnum?")</param>
internal sealed record PropertyTypeData(
    string Type,
    bool HasTypeMapping,
    bool HasGenericTypeMapping,
    bool NeedsSpreadAssignment,
    bool IsEnum,
    bool IsNullable,
    string? EnumCollectionElementType = null
    );
