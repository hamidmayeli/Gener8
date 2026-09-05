namespace Gener8.Contexts;

// A nested model property being inlined into the DTO by Flatten.
internal readonly record struct FlattenParent(string Name, string TypeFullName, bool IsNullable);
