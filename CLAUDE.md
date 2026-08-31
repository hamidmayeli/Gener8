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
│   ├── SourceProducer.cs                 — Emits partial class, extension methods, concrete repository
│   ├── SyntaxTransformer.cs              — Roslyn pipeline: predicate + ExtractClassTarget
│   ├── PropertyDataBuilder.cs            — builds PropertyData list from a model symbol
│   ├── DefaultSource.cs                  — placeholder (all types moved to Abstractions)
│   ├── DefaultSource.DynamoDb.cs         — placeholder (moved to Extensions.DynamoDB)
│   └── Contexts/                         — immutable records used across the incremental pipeline
│       ├── TargetClass.cs
│       ├── ModelClass.cs
│       ├── PropertyData.cs
│       ├── PropertyTypeData.cs
│       ├── FlattenedPropertyData.cs
│       ├── FlattenPrefixMode.cs          — internal enum: Parent, None, Gaped
│       └── RepositoryKind.cs             — internal enum: None, DynamoDb, MongoDb, Custom
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
   - `{Namespace}.{ClassName}Repository.g.cs` — concrete repository (when `Repository` is set)

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
- DynamoDB/MongoDB abstract collection interfaces are remapped to `List<T>` in the DTO; `ToDto` uses collection spread
- DynamoDB: `enum` properties get `[DynamoDBProperty(typeof(EnumToStringConverter<T>))]` (from `Gener8.Converters` in `Gener8.Extensions.DynamoDB`)
- MongoDB: `enum` properties get `[BsonRepresentation(BsonType.String)]`
- `Repository = RepositoryType.DynamoDb|MongoDb|Custom` — generates a concrete `{Model.Name}Repository` class; base classes come from `Gener8.Extensions.DynamoDB`, `Gener8.Extensions.MongoDB`, or `Gener8.Abstractions` respectively
- `ForceNullable = [...]` — makes non-nullable model properties nullable in the DTO
- `DtoNamespaces = [...]` — qualifying namespaces for auto type mapping
- Generator targets `netstandard2.0`; uses Roslyn incremental API (`IIncrementalGenerator`)

## Build, packaging, and CI

See [docs/contributing.md](docs/contributing.md) for build commands, NuGet packaging, and CI/CD details.
# userEmail
The user's email address is hamid.mayeli@justeattakeaway.com. Use it only to identify the user, such as for authorship, attribution, or filtering their own work. Never send it to an unrelated service, such as in a request header, URL, or payload, unless the user explicitly asks.
# currentDate
Today's date is 2026-08-31.

      IMPORTANT: this context may or may not be relevant to your tasks. You should not respond to this context unless it is highly relevant to your task.
