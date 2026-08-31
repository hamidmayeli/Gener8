# How It Works

## Overview

`Gener8` is a Roslyn **incremental source generator** (`IIncrementalGenerator`). It participates in the compiler's incremental computation graph, meaning it only reruns the parts of its logic that are affected by a given change. For large projects this keeps rebuild times fast.

The generator runs entirely at **compile time** — it reads the semantic model of your C# code and emits new source files that are compiled alongside your own code. There is no reflection, no runtime code generation, and no performance overhead in the deployed application.

---

## Package architecture

```
Gener8 (NuGet)
├── analyzers/dotnet/cs/Gener8.dll          — the Roslyn source generator
└── lib/netstandard2.0/Gener8.Abstractions.dll  — attributes, enums, interfaces, base classes

Gener8.Extensions.DynamoDB (NuGet)           — optional, for RepositoryType.DynamoDb
└── lib/netstandard2.0/Gener8.Extensions.DynamoDB.dll
    ├── Gener8.Converters.EnumToStringConverter<T>
    ├── Gener8.Converters.NullableEnumToStringConverter<T>
    ├── Gener8.Converters.EnumListToStringListConverter<T>
    ├── Gener8.Converters.NullableEnumListToStringListConverter<T>
    ├── Gener8.IDynamoDbRepositoryContext
    └── Gener8.DynamoDbRepository<TModel, TDto>

Gener8.Extensions.MongoDB (NuGet)            — optional, for RepositoryType.MongoDb
└── lib/netstandard2.0/Gener8.Extensions.MongoDB.dll
    ├── Gener8.IMongoDbRepositoryContext
    └── Gener8.MongoDbRepository<TModel, TDto>
```

`Gener8.Abstractions` is also published as a standalone package. Its types are available to consumers via the core `Gener8` package without a separate install.

---

## Generator pipeline

```
SyntaxProvider.CreateSyntaxProvider
  predicate  → IsPartialClassWithAttributes  (syntax-only fast filter)
  transform  → ExtractClassTarget            (semantic analysis)
  .Where(not null)
                         ↓
        ┌────────────────┴────────────────────────────────┐
        │                                                  │
RegisterSourceOutput                          .SelectMany(t => t.AutoDtoTargets)
→ Emit (per-DTO: model + extensions           .Collect()
        + concrete repository)                     │
                                             RegisterSourceOutput
                                             → EmitAutoDtos
```

> **Note**: `RegisterPostInitializationOutput` is no longer used. All attributes, enums, and repository contracts that were previously injected as source come from `Gener8.Abstractions.dll` (included in the `Gener8` NuGet under `lib/netstandard2.0/`). Similarly, SDK-specific base classes and converters come from the extension packages — the generator no longer emits these as source files.

### Stage 1 — Syntax filter (`IsPartialClassWithAttributes`)

The predicate runs on every syntax node in the compilation. It performs only **syntax-level** checks (no semantic model access, which would be expensive):

1. Is it a `ClassDeclarationSyntax`?
2. Does it have at least one attribute list?
3. Does it carry the `partial` modifier?

Any class that fails these checks is discarded immediately. This keeps the expensive semantic stage lean.

### Stage 2 — Semantic transform (`ExtractClassTarget`)

For each class that passes the filter, the generator accesses the semantic model:

1. **Resolve the DTO class symbol** — obtains the `INamedTypeSymbol` for the partial class.
2. **Find `[FromModel]`** — locates the attribute (resolved from `Gener8.Abstractions.dll`) and reads the `typeof()` argument to get the model type symbol.
3. **Extract configuration** — reads `Ignore`, `Flatten`, `FlattenPrefix`, `IncludeInherited`, `DtoNamespaces`, `ForceNullable` from the attribute; reads `[TypeMapping]`, `[RenameProperty]`, and `[IgnoreTypeMapping]` attributes from the class.
4. **Detect constructor params** — `PropertyDataBuilder` inspects the model's non-implicit constructors. If a constructor is found whose parameters all resolve to public properties (by exact name or camelCase→PascalCase), those property names are recorded as constructor-backed. This drives two downstream effects: the properties are included even if get-only on the model, and `ToModel` uses constructor-style initialization.
4b. **Infer type mappings** — when `DtoNamespaces` is set, `PropertyDataBuilder` scans model properties and automatically adds type mappings for any type whose namespace is in the qualifying set, unless excluded by `[IgnoreTypeMapping]` or already covered by an explicit `[TypeMapping]`. Each inferred mapping also registers the type as an **auto-target** — the transformer synthesises a `TargetClass` for it and returns it in `AutoDtoTargets`. The pipeline emits these companion DTOs as extra source files.

5. **Walk model properties** (`GetModelProperties`) — iterates public non-static instance properties, skipping get-only ones unless they are constructor-backed. A `HashSet<string>` tracks already-seen names for `IncludeInherited = true`. Traversal stops at `System.Object`.
6. **Build `PropertyData` records** — delegates to `PropertyDataBuilder`, which resolves the type display string, applies any type mapping (including abstract-collection-to-`List<T>` remapping for DynamoDB), applies any rename, and reads getter/setter/init/required/initializer flags. Constructor-backed get-only properties are forced to `IsInitOnly = true`.
7. **Handle `Flatten`** — for each property in the flatten list, recursively walks the nested type's properties (one level only), applies prefix logic and type mappings, and emits each as a top-level `PropertyData` with a `FlattenedPropertyData` sub-record.
8. **Returns a `TargetClass` record** — an immutable snapshot of everything the `Emit` stage needs.

### Stage 3 — Code generation (`Emit`)

Takes a `TargetClass` and writes up to three `StringBuilder`-based C# source files.

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

For DynamoDB repositories, enum properties get `[DynamoDBProperty(typeof(EnumToStringConverter<T>))]` (from `Gener8.Converters`). For MongoDB, enum properties get `[BsonRepresentation(BsonType.String)]`.

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
      public static {ModelFullName}? ToModel(this {ClassName}? dto) => ...;

      [return: NotNullIfNotNull(nameof(model))]
      public static {ClassName}? ToDto(this {ModelFullName}? model) => ...;
  }
[}]
```

**`EmitRepository`** (only when `target.Repository != RepositoryKind.None`) writes a concrete repository class (`{ClassName}Repository.g.cs`). The generated class inherits from `Gener8.DynamoDbRepository<TModel, TDto>`, `Gener8.MongoDbRepository<TModel, TDto>`, or `Gener8.RepositoryBase<TModel, TDto>` — these types now come from the extension packages or `Gener8.Abstractions`, not injected source.

---

## Internal types

All records live under the `Gener8.Contexts` namespace and use value-equality semantics for Roslyn incremental caching.

See the source files under `src/Gener8/Contexts/` for full details of `TargetClass`, `ModelClass`, `PropertyData`, `PropertyTypeData`, and `FlattenedPropertyData`.

---

## Incremental caching

Because the transform returns immutable records with value-equality semantics (C# `record` types), Roslyn can cache results between compilations. If a source file that contributed a `TargetClass` has not changed, the cached record is reused and `Emit` is not re-invoked.

---

## `IsExternalInit` polyfill

The generator itself targets `netstandard2.0` to be compatible with all Roslyn host versions. Because `init`-only setters require the `IsExternalInit` marker type (introduced in .NET 5), the project includes `IsExternalInit.cs` — a minimal stub that satisfies the compiler without requiring a newer runtime.

---

## `netstandard2.0` target

Source generators must target `netstandard2.0` because the Roslyn SDK that hosts them may run on older .NET Framework versions (e.g., in Visual Studio on Windows). The consuming project can target any framework.

`Gener8.Abstractions`, `Gener8.Extensions.DynamoDB`, and `Gener8.Extensions.MongoDB` also target `netstandard2.0` to ensure broad compatibility.
