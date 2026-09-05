namespace Gener8.Contexts;

// A model property that can only be populated through a constructor parameter.
internal readonly record struct ConstructorBackedProperty(string Name, string? DefaultValue);
