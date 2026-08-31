# How It Works

## Overview

`Gener8` is a Roslyn **incremental source generator** (`IIncrementalGenerator`). It participates in the compiler's incremental computation graph, meaning it only reruns the parts of its logic that are affected by a given change. For large projects this keeps rebuild times fast.

The generator runs entirely at **compile time** — it reads the semantic model of your C# code and emits new source files that are compiled alongside your own code. There is no reflection, no runtime code generation, and no performance overhead in the deployed application.

---

## Generator pipeline

```
RegisterPostInitializationOutput
  → injects FromModelAttribute, TypeMappingAttribute, RenamePropertyAttribute,
    FlattenPrefix, IRepository<T> into every consumer

SyntaxProvider.CreateSyntaxProvider
  predicate  → IsPartialClassWithAttributes  (syntax-only fast filter)
  transform  → ExtractClassTarget            (semantic analysis)
  .Where(not null)
                             ↓
            ┌────────────────┴────────────────────────────────┐
            │                                                  │
  RegisterSourceOutput                         .Select(t => t.Repository)
  → Emit (per-DTO: model + extensions          .Where(k => k != None)
          + concrete repository)               .Collect()
                                                      │
                                               RegisterSourceOutput
                                               → EmitRepositoryBaseClasses
                                                 (once per kind per compilation)
```

### Stage 1 — Attribute injection

Before any user code is examined, the generator calls `RegisterPostInitializationOutput` to inject source files into the consumer's compilation:

| File | Contents |
|---|---|
| `RepositoryType.g.cs` | `RepositoryType` enum (`None`, `DynamoDb`, `MongoDb`, `Custom`) |
| `FlattenPrefix.g.cs` | `FlattenPrefix` enum |
| `FromModelAttribute.g.cs` | `[FromModel]` attribute class (references both enums) |
| `TypeMappingAttribute.g.cs` | `[TypeMapping]` attribute class |
| `RenamePropertyAttribute.g.cs` | `[RenameProperty]` attribute class |
| `IgnoreTypeMappingAttribute.g.cs` | `[IgnoreTypeMapping]` attribute class |
| `IRepository.g.cs` | `Gener8.IRepository<TModel>` interface |

The SDK-heavy abstract base classes (`DynamoDbRepository<TModel, TDto>` and `MongoDbRepository<TModel, TDto>`) are **not** injected here. They are emitted conditionally in a separate `RegisterSourceOutput` step after all DTOs have been examined, ensuring they appear at most once per compilation and only when needed.

This means consumers never need a separate runtime package — all injected types exist only during compilation.

### Stage 2 — Syntax filter (`IsPartialClassWithAttributes`)

The predicate runs on every syntax node in the compilation. It performs only **syntax-level** checks (no semantic model access, which would be expensive):

1. Is it a `ClassDeclarationSyntax`?
2. Does it have at least one attribute list?
3. Does it carry the `partial` modifier?

Any class that fails these checks is discarded immediately. This keeps the expensive semantic stage lean.

### Stage 3 — Semantic transform (`ExtractClassTarget`)

For each class that passes the filter, the generator accesses the semantic model:

1. **Resolve the DTO class symbol** — obtains the `INamedTypeSymbol` for the partial class.
2. **Find `[FromModel]`** — locates the attribute and reads the `typeof()` argument to get the model type symbol.
3. **Extract configuration** — reads `Ignore`, `Flatten`, `FlattenPrefix`, `IncludeInherited`, `DtoNamespaces`, `ForceNullable` from the attribute; reads `[TypeMapping]`, `[RenameProperty]`, and `[IgnoreTypeMapping]` attributes from the class.
4. **Detect constructor params** — `PropertyDataBuilder` inspects the model's non-implicit constructors. If a constructor is found whose parameters all resolve to public properties (by exact name or camelCase→PascalCase), those property names are recorded as constructor-backed. This drives two downstream effects: the properties are included even if get-only on the model, and `ToModel` uses constructor-style initialization.
4b. **Infer type mappings** — when `DtoNamespaces` is set, `PropertyDataBuilder` scans model properties and automatically adds type mappings for any type whose namespace is in the qualifying set, unless it is excluded by `[IgnoreTypeMapping]` or already covered by an explicit `[TypeMapping]`. Each inferred mapping also registers the type as an **auto-target** — the transformer synthesises a `TargetClass` for it (using the same accessibility and namespace as the DTO) and returns it in `AutoDtoTargets`. The pipeline emits these companion DTOs as extra source files.

5. **Walk model properties** (`GetModelProperties`) — iterates public non-static instance properties, skipping get-only ones unless they are constructor-backed. A `HashSet<string>` tracks already-seen names to handle overrides when `IncludeInherited = true`. Traversal stops at `System.Object`.
6. **Build `PropertyData` records** — delegates to `PropertyDataBuilder`, which resolves the type display string, applies any type mapping (including abstract-collection-to-`List<T>` remapping for DynamoDB), applies any rename, and reads getter/setter/init/required/initializer flags. Constructor-backed get-only properties are forced to `IsInitOnly = true` so the DTO emits `init`.
7. **Handle `Flatten`** — for each property in the flatten list, recursively walks the nested type's properties (one level only), applies prefix logic and type mappings, and emits each as a top-level `PropertyData` with a `FlattenedPropertyData` sub-record (carrying the parent name, fully-qualified parent type, and nested property name needed for `ToModel` reconstruction).
8. **Returns a `TargetClass` record** — an immutable snapshot of everything the `Emit` stage needs, including `ModelClass.PrimaryConstructorParams` (the ordered property names for constructor-style `ToModel`).

### Stage 4 — Code generation (`Emit`)

Takes a `ClassTarget` and writes up to three `StringBuilder`-based C# source files.

**`EmitModel`** writes the partial class (`{ClassName}.g.cs`):

```
// <auto-generated/>
#nullable enable

[namespace X {]
  {accessibility} partial class {ClassName}
  {
      public [required ]{type} {name} { [get;] [set;|init;] }[ = {initializer};]
      ...
  }
[}]
```

**`EmitExtensions`** writes the mapping helpers (`{ClassName}Extensions.g.cs`):

```
// <auto-generated/>
#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Linq;

[namespace X {]
  {extAccessibility} static class {ClassName}Extensions
  {
      [return: NotNullIfNotNull(nameof(dto))]
      public static {ModelFullName}? ToModel(this {ClassName}? dto)
          // constructor style when PrimaryConstructorParams is set:
          => dto is null ? null : new(dto.P1, dto.P2);
          // or object-initializer style otherwise:
          => dto is null ? null : new {ModelFullName} { ... };

      [return: NotNullIfNotNull(nameof(model))]
      public static {ClassName}? ToDto(this {ModelFullName}? model)
          => model is null ? null : new {ClassName} { ... };
  }
[}]
```

**`ToModel` style selection** — if `ModelClass.PrimaryConstructorParams` is set, `ToModel` emits `new(dto.P1, dto.P2, ...)` (positional constructor call) for the constructor-param properties. Any remaining settable properties are emitted as an object-initializer block after the constructor call: `new(dto.Name) { Order = dto.Order, }`. When `PrimaryConstructorParams` is not set, the full object-initializer form is used.

Type-mapped properties generate chained calls (`dto.Prop.ToModel()` / `model.Prop.ToDto()`), with `?.` for nullable types. Flattened properties appear in `ToDto` via their `ReadPath`. In `ToModel`, flattened properties are grouped by parent and the nested object is reconstructed inline (`Parent = new ParentType { Nested = dto.FlatProp, ... }`); nullable parents get a null-safe ternary (`dto.FlatProp is null ? null : new ParentType { ... }`). Renamed properties use `ModelPropertyName` on the model side. DynamoDB/MongoDB abstract collection and array properties use collection spread (`[.. model.Prop]`) in `ToDto`.

**`EmitRepository`** (only when `target.Repository != RepositoryKind.None`) writes a concrete repository class (`{ClassName}Repository.g.cs`):

```
// <auto-generated/>
#nullable enable

[namespace X {]
  {accessibility} partial class {ClassName}Repository
      : Gener8.DynamoDbRepository<{ModelFullName}, {ClassName}>   // or MongoDbRepository
  {
      // DynamoDb: public {ClassName}Repository(IDynamoDbRepositoryContext context) : base(context) {}
      // MongoDb:  public {ClassName}Repository(IMongoDbRepositoryContext context) : base(context, "{ClassName}") {}

      protected override {ModelFullName} ToModel({ClassName} dto)    => dto.ToModel();
      protected override {ClassName}     ToDto({ModelFullName} model) => model.ToDto();
  }
[}]
```

**`EmitRepositoryBaseClasses`** is called once per compilation (via a `.Collect()`-based `RegisterSourceOutput`) and emits `DynamoDbRepository.g.cs` and/or `MongoDbRepository.g.cs` only if at least one DTO in the compilation requested each kind.

The hint name for each file is `{Namespace}.{ClassName}[Extensions|Repository].g.cs` (or without namespace prefix for the global namespace).

---

## Internal types

All records live under the `Gener8.Contexts` namespace and use value-equality semantics for Roslyn incremental caching.

### `TargetClass`

Carries everything `Emit` needs for one DTO:

```csharp
record TargetClass(
    string ClassName,
    string? Namespace,
    string Accessibility,
    IReadOnlyCollection<PropertyData> Properties,
    ModelClass Model,
    RepositoryKind Repository,
    IReadOnlyCollection<TargetClass> AutoDtoTargets); // companion DTOs inferred via DtoNamespaces

record ModelClass(
    string FullName,                                  // global::-prefixed fully-qualified name
    string Name,                                      // simple class name, used for the repository class name
    ImmutableArray<string> PrimaryConstructorParams); // ordered property names for ctor-style ToModel; default = use object initializer
```

`RepositoryKind` is an internal enum (`None`, `DynamoDb`, `MongoDb`, `Custom`) — a mirror of the injected `RepositoryType` enum that avoids a dependency on the generated attribute source.

### `PropertyData`

Per-property code-generation data, composed of two sub-records:

```csharp
record PropertyData(
    PropertyTypeData TypeData,
    string Name,
    bool HasGetter,
    bool HasSetter,
    bool IsInitOnly,
    bool IsRequired,
    string? Initializer,
    string? ModelPropertyName,  // original model name when [RenameProperty] was applied; null = same as Name
    bool IsUserDeclared,        // true when the DTO already declares this property; skip in EmitModel, keep in mappings
    FlattenedPropertyData? Flattened);  // non-null only for properties introduced via Flatten

record PropertyTypeData(
    string Type,
    bool HasTypeMapping,              // true when the type was remapped (via [TypeMapping] or collection/array → List<T>)
    bool HasGenericTypeMapping,       // true when the remapping was a collection/array → List<T> (not a direct [TypeMapping])
    bool NeedsSpreadAssignment,       // true when an abstract collection was remapped to List<T> (DynamoDB/MongoDB)
    bool IsEnum,                      // true when the property is an enum type (used for DynamoDB/MongoDB annotations)
    bool IsNullable,                  // true when the property type is nullable
    string? EnumCollectionElementType); // non-null for IList<TEnum> / IList<TEnum?>: holds "EnumType" or "EnumType?"

record FlattenedPropertyData(
    string ReadPath,            // model-side read expression, e.g. "Address?.Street"
    string ParentName,          // parent property name, e.g. "ShippingAddress"
    string ParentTypeFullName,  // global::-prefixed parent type for 'new ParentType { }' in ToModel
    string NestedPropertyName,  // property name on the nested type, e.g. "Street"
    bool OriginallyNullable);   // true when nested type was nullable before parent nullability was applied
```

---

## Incremental caching

Because the transform returns immutable records with value-equality semantics (C# `record` types), Roslyn can cache results between compilations. If a source file that contributed a `TargetClass` has not changed, the cached record is reused and `Emit` is not re-invoked — only the changed inputs flow through the pipeline.

---

## `IsExternalInit` polyfill

The generator itself targets `netstandard2.0` to be compatible with all Roslyn host versions. Because `init`-only setters require the `IsExternalInit` marker type (introduced in .NET 5), the project includes `IsExternalInit.cs` — a minimal stub that satisfies the compiler without requiring a newer runtime.

---

## `netstandard2.0` target

Source generators must target `netstandard2.0` because the Roslyn SDK that hosts them may run on older .NET Framework versions (e.g., in Visual Studio on Windows). The consuming project can target any framework.
