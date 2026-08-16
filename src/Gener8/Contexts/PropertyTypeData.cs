namespace Gener8.Contexts;

internal sealed record PropertyTypeData(
    string Type,
    bool HasTypeMapping,       // true when the property type was remapped via TypeMappingAttribute
    bool NeedsSpreadAssignment // true when model type is abstract collection (e.g. IReadOnlyCollection<T>) and DTO uses List<T>
    );
