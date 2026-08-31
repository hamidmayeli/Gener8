namespace Gener8.Contexts;

internal sealed record PropertyData(
    PropertyTypeData TypeData,
    string Name,
    bool HasGetter,
    bool HasSetter,
    bool IsInitOnly,
    bool IsRequired,
    string? Initializer,
    string? ModelPropertyName,      // original model property name when renamed; null = same as Name
    bool IsUserDeclared,            // true when the DTO already declares this property; skip in EmitModel, keep in mappings
    FlattenedPropertyData? Flattened,
    bool IsForceNullable = false,   // true when the property was made nullable via ForceNullable = [...]
    string? ForceNullableModelType = null  // globally-qualified model type for the Get{Prop} partial method stub
);
