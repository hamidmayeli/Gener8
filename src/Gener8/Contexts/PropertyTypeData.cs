namespace Gener8.Contexts;

internal sealed record PropertyTypeData(
    string Type,
    bool HasTypeMapping,            // true when the property type or its supported generic argument was remapped via TypeMappingAttribute
    bool HasGenericTypeMapping,     // true when a supported generic argument (e.g. List<T>'s T) was remapped via TypeMappingAttribute
    bool NeedsSpreadAssignment      // true when model type is abstract collection (e.g. IReadOnlyCollection<T>) and DTO uses List<T>
    );
