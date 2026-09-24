# Gener8

C# source generator that copies public properties from a model class to a decorated partial DTO class.

## Structure

```
Gener8.slnx
src/
├── Gener8.Abstractions/                  — core types, zero deps (netstandard2.0)
│   ├── Gener8.Abstractions.csproj
│   ├── FromModelAttribute.cs
│   ├── TypeMappingAttribute.cs
│   ├── RenamePropertyAttribute.cs
│   ├── IgnoreTypeMappingAttribute.cs
│   ├── RepositoryType.cs                 — enum: None, DynamoDb, MongoDb, Custom
│   ├── FlattenPrefix.cs                  — enum: Parent, None, Gaped
│   ├── IRepository.cs                    — IRepository<TModel> with 5 CRUD methods
│   ├── ICompositeKeyRepository.cs        — extends IRepository<TModel> with composite-key overloads
│   ├── IRepositoryContext.cs             — empty marker interface for Custom repositories
│   └── RepositoryBase.cs                 — abstract base for Custom repositories
├── Gener8/                               — Roslyn source generator (netstandard2.0)
│   ├── Gener8.csproj                     — packs generator in analyzers/ + Abstractions DLL in lib/
│   ├── FromModelGenerator.cs             — IIncrementalGenerator implementation
│   ├── SyntaxTransformer.cs              — Roslyn pipeline: predicate + ExtractClassTarget
│   ├── SourceProducer.cs                 — Emits partial class, extension methods, concrete repository
│   ├── Diagnostics.cs                    — GEN001–GEN007 + GEN999 descriptors
│   ├── BuildDiagnostic.cs                — immutable diagnostic payload used by builders
│   ├── RepositoryProfile.cs              — per-backend differences (usings, attributes, base class)
│   ├── TypeNames.cs                      — display-string helpers + the model→DTO name convention
│   ├── IsExternalInit.cs                 — polyfill for init-only setters on netstandard2.0
│   ├── Compatibility/
│   │   └── NotNullWhenAttribute.cs       — polyfill for [NotNullWhen] on netstandard2.0
│   ├── ContextBuilders/
│   │   ├── AttributeReader.cs            — TypedConstant extraction for every attribute argument
│   │   ├── ConstructorMatcher.cs         — ctor-parameter ↔ property matching (order + defaults)
│   │   ├── DefaultSource.cs              — fully-qualified attribute name constants
│   │   ├── EnumTypes.cs                  — enum / enum-collection classification
│   │   ├── KnownCollections.cs           — supported collection type list
│   │   ├── ModelPropertyReader.cs        — the single base-type walk over model properties
│   │   ├── OnlyIncludeFilter.cs          — OnlyInclude whitelist (null-object: None = include all)
│   │   ├── PropertyBuildResult.cs        — builder output: properties, auto-targets, diagnostics
│   │   ├── PropertyDataBuilder.cs        — orchestrates one DTO's PropertyData list
│   │   ├── PropertyTypeResolver.cs       — applies mapping rules, nullability, repository needs
│   │   ├── RepositoryCollectionRemapper.cs — abstract collection interface → concrete type
│   │   ├── TypeMappingResolver.cs        — explicit [TypeMapping] + DtoNamespaces inference
│   │   └── TypeMapping/
│   │       ├── ITypeMappingRule.cs       — mapping rule abstraction
│   │       ├── DirectMappingRule.cs      — direct type mapping rule
│   │       ├── CollectionMappingRule.cs  — collection element mapping rule
│   │       ├── ArrayMappingRule.cs       — array element mapping rule
│   │       └── DictionaryMappingRule.cs  — dictionary key/value type mapping rule
│   └── Contexts/                         — immutable records used across the incremental pipeline
│       ├── AutoDtoTarget.cs
│       ├── ClassTargetResult.cs
│       ├── CollectionTypeMapping.cs
│       ├── ConstructorBackedProperty.cs
│       ├── FlattenedPropertyData.cs
│       ├── FlattenParent.cs
│       ├── FlattenPrefixMode.cs          — internal enum: Parent, None, Gaped
│       ├── FromModelOptions.cs
│       ├── MappedType.cs
│       ├── ModelClass.cs
│       ├── PropertyBuildRequest.cs
│       ├── PropertyData.cs
│       ├── PropertyTypeData.cs
│       ├── RepositoryKind.cs             — internal enum: None, DynamoDb, MongoDb, Custom
│       ├── TargetClass.cs
│       └── TypeMappingContext.cs
├── Gener8.Extensions.DynamoDB/           — DynamoDB integration (netstandard2.0, requires AWSSDK.DynamoDBv2)
│   ├── Gener8.Extensions.DynamoDB.csproj
│   ├── Converters/
│   │   ├── EnumToStringConverter.cs
│   │   ├── NullableEnumToStringConverter.cs
│   │   ├── EnumListToStringListConverter.cs
│   │   └── NullableEnumListToStringListConverter.cs
│   └── Repositories/
│       ├── IDynamoDbRepositoryContext.cs
│       └── DynamoDbRepository.cs
└── Gener8.Extensions.MongoDB/            — MongoDB integration (netstandard2.0, requires MongoDB.Driver)
    ├── Gener8.Extensions.MongoDB.csproj
    └── Repositories/
        ├── IMongoDbRepositoryContext.cs
        └── MongoDbRepository.cs
```

## How it works

1. `Gener8.Abstractions.dll` is bundled in the `Gener8` NuGet under `lib/netstandard2.0/`. Consumers get `[FromModel]`, `RepositoryType`, `IRepository<T>`, etc. as real compiled types with IntelliSense and Go-to-Definition support — no source injection needed.
2. Consumers decorate a `partial class` with `[FromModel(typeof(TheModel))]`
3. Generator copies public instance properties and emits up to three files per DTO:
   - `{Namespace}.{ClassName}.g.cs` — the partial class with copied properties
   - `{Namespace}.{ClassName}Extensions.g.cs` — `ToModel` / `ToDto` extension methods
   - `{Namespace}.{ModelName}Repository.g.cs` — concrete repository (when `Repository` is set)

## NuGet packages

| Package | Install when… |
|---|---|
| `Gener8` | Always (generator + abstractions) |
| `Gener8.Extensions.DynamoDB` | Using `RepositoryType.DynamoDb` |
| `Gener8.Extensions.MongoDB` | Using `RepositoryType.MongoDb` |
| `Gener8.Abstractions` | Referencing from a shared contracts project (standalone) |

## Usage

```csharp
public class TheModel { public string Name { get; set; } }

[FromModel(typeof(TheModel))]
internal partial class TheDto {}

// Generator emits TheDto.g.cs:
// internal partial class TheDto { public string Name { get; set; } }

// Generator emits TheDtoExtensions.g.cs:
// internal static class TheDtoExtensions
// {
//     [return: NotNullIfNotNull(nameof(dto))]
//     public static TheModel? ToModel(this TheDto? dto)
//         => dto is null ? null : new TheModel { Name = dto.Name };
//
//     [return: NotNullIfNotNull(nameof(model))]
//     public static TheDto? ToDto(this TheModel? model)
//         => model is null ? null : new TheDto { Name = model.Name };
// }
```

## Generator internals

`SyntaxTransformer` calls `PropertyDataBuilder.Build(request)` once per DTO and gets back a
`PropertyBuildResult`. Diagnostics are returned as data (`BuildDiagnostic`), not accumulated on
the builder, so the transformer reports them in one loop and child auto-DTO diagnostics surface
alongside the parent's.

Conventions worth knowing before editing:

- **Attribute arguments** are read only through `AttributeReader`. Do not hand-roll a
  `NamedArguments` loop.
- **Model properties** are enumerated only through `ModelPropertyReader`. `ReadSettable` is the
  set a DTO can populate; `ReadNames` is every public instance property.
- **Constructor matching** lives only in `ConstructorMatcher`. Both the property builder (for
  parameter defaults) and `ModelClass.PrimaryConstructorParams` (for argument order) project
  from it, so they cannot disagree.
- **Type display strings** are manipulated only through `TypeNames`. The trailing `?` and the
  model→DTO method-name convention (`Product` + `ProductView` → `ToView`) are defined there.
- **Type mapping** is keyed on the non-nullable display string; `TypeMappingResolver` normalises
  every lookup. Adding a new mapping shape means adding an `ITypeMappingRule` to the ordered
  list in `PropertyTypeResolver`, not another guard clause.
- **Repository backends** differ only inside `RepositoryProfile`. Adding one means adding a
  profile subclass and a line in `RepositoryProfile.For`.

## Key details

- Attribute full name: `Gener8.FromModelAttribute` (defined in `Gener8.Abstractions.dll`)
- Model lookup: resolved directly from the `typeof()` argument — fully-qualified names supported
- Property filter: public, non-static, must have `set` or `init` (get-only excluded unless constructor-backed)
- Accessor kinds preserved: `set`, `init`; constructor-backed get-only properties emitted with forced `init`
- Constructor mapper: when the model has a non-implicit constructor whose parameters all match public property names (exact or camelCase→PascalCase), `ToModel` emits `new(dto.P1, dto.P2, ...)` instead of object-initializer syntax
- `[TypeMapping(typeof(A), typeof(ADto))]` — remaps property types; extension methods chain `.ToModel()`/`.ToDto()`
- `[IgnoreTypeMapping(typeof(T))]` — suppresses auto type mapping for `T` when `DtoNamespaces` is active
- `[RenameProperty("OldName", "NewName")]` — renames in DTO; extensions use correct name on each side
- `Flatten = [...]` — inlines nested properties; `ToModel` reconstructs the nested parent (null-safe for nullable parents)
- Dictionary support: `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`, `SortedList<K,V>`, `SortedDictionary<K,V>` are supported; key and value types participate in type mapping when in a qualifying namespace; extension methods use `ToDictionary(...)` when either argument is remapped
- DynamoDB only: abstract collection interfaces are remapped to `List<T>` (`ISet<T>` to `HashSet<T>`) in the DTO because the AWS SDK instantiates the DTO itself; `ToDto` then uses collection spread. Abstract dictionary interfaces (`IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`) are remapped to `Dictionary<K,V>`; `ToDto` uses `ToDictionary(...)`. MongoDB needs no remapping — its driver can instantiate abstract collection/dictionary types
- GEN006: raised (error) when a DynamoDB DTO has a dictionary property with a non-string key type
- DynamoDB: `enum` properties get `[DynamoDBProperty(typeof(EnumToStringConverter<T>))]` (from `Gener8.Converters` in `Gener8.Extensions.DynamoDB`)
- MongoDB: `enum` properties get `[BsonRepresentation(BsonType.String)]`
- `Repository = RepositoryType.DynamoDb|MongoDb|Custom` — generates a concrete `{Model.Name}Repository` class; base classes come from `Gener8.Extensions.DynamoDB`, `Gener8.Extensions.MongoDB`, or `Gener8.Abstractions` respectively
- `ForceNullable = [...]` — makes non-nullable model properties nullable in the DTO
- `DtoNamespaces = [...]` — qualifying namespaces for auto type mapping
- Generator targets `netstandard2.0`; uses Roslyn incremental API (`IIncrementalGenerator`)

## Build, packaging, and CI

See [docs/contributing.md](docs/contributing.md) for build commands, NuGet packaging, and CI/CD details.
# userEmail
The user's email address is hamid.mayeli@yahoo.com. Use it only to identify the user, such as for authorship, attribution, or filtering their own work. Never send it to an unrelated service, such as in a request header, URL, or payload, unless the user explicitly asks.
# currentDate
Today's date is 2026-08-31.

      IMPORTANT: this context may or may not be relevant to your tasks. You should not respond to this context unless it is highly relevant to your task.
