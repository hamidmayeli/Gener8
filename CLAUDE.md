# Gener8

C# source generator that copies public properties from a model class to a decorated partial DTO class.

## Structure

```
Gener8.slnx
src/Gener8/
├── Gener8.csproj            — netstandard2.0, Roslyn packages as private assets
├── FromModelGenerator.cs    — IIncrementalGenerator implementation
├── SourceProducer.cs        — Emits the partial class, extension methods, and optional repository
├── SyntaxTransformer.cs     — Roslyn pipeline: predicate + ExtractClassTarget (delegates to PropertyDataBuilder)
├── PropertyDataBuilder.cs   — builds the PropertyData list from a model symbol
├── DefaultSource.cs         — injected attribute/enum/base-class source files
└── Contexts/                — immutable records used across the incremental pipeline
    ├── TargetClass.cs       — record: ClassName, Namespace, Accessibility, Properties, Model, Repository
    ├── ModelClass.cs        — record: FullName, Name
    ├── PropertyData.cs      — record: TypeData, Name, accessors, ModelPropertyName, IsUserDeclared, Flattened
    ├── PropertyTypeData.cs  — record: Type, HasTypeMapping, HasGenericTypeMapping, NeedsSpreadAssignment, IsEnum, IsNullable, EnumCollectionElementType
    ├── FlattenedPropertyData.cs — record: ReadPath, ParentName, ParentTypeFullName, NestedPropertyName, OriginallyNullable
    ├── FlattenPrefixMode.cs — internal enum: Parent, None, Gaped
    └── RepositoryKind.cs    — internal enum: None, DynamoDb, MongoDb, Custom
```

## How it works

1. `RegisterPostInitializationOutput` injects `FromModelAttribute` (and other attributes) into consumer projects — no separate runtime reference needed
2. Consumers decorate a `partial class` with `[FromModel(typeof(TheModel))]`
3. Generator copies public instance properties (preserving `get`/`set`/`init`, `required`, initializers) and emits up to three files per DTO:
   - `{Namespace}.{ClassName}.g.cs` — the partial class with copied properties
   - `{Namespace}.{ClassName}Extensions.g.cs` — `ToModel` / `ToDto` extension methods
   - `{Namespace}.{ClassName}Repository.g.cs` — concrete repository (when `Repository` is set)

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

- Attribute full name: `Gener8.FromModelAttribute`
- Model lookup: resolved directly from the `typeof()` argument — fully-qualified names supported
- Property filter: public, non-static only
- All accessor kinds preserved: `get`, `set`, `init`
- `[TypeMapping(typeof(A), typeof(ADto))]` — remaps property types; extension methods generate chained `.ToModel()`/`.ToDto()` calls
- `[RenameProperty("OldName", "NewName")]` — renames in DTO; extensions use correct name on each side
- `Flatten = [...]` — inlines nested properties; flattened props appear in `ToDto` (via path); `ToModel` reconstructs the nested parent object from the spread DTO properties (null-safe ternary for nullable parents)
- Flattened + type-mapped properties are skipped in both extension methods (cannot chain through a flattened path)
- DynamoDB/MongoDB abstract collection interfaces (`IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, `IList<T>`, `ICollection<T>`) and arrays (`T[]`) are automatically remapped to `List<T>` in the DTO; `ToDto` uses collection spread `[.. model.Prop]`
- DynamoDB: `enum` properties automatically get `[DynamoDBProperty(typeof(EnumToStringConverter<T>))]`; nullable enum (`T?`) gets `NullableEnumToStringConverter<T>`; `IList<TEnum>` / `IList<TEnum?>` gets `EnumListToStringListConverter<T>` / `NullableEnumListToStringListConverter<T>`
- MongoDB: `enum` properties automatically get `[BsonRepresentation(BsonType.String)]`
- `Repository = RepositoryType.DynamoDb|MongoDb|Custom` — generates a concrete `{ClassName}Repository` class (partial, matching DTO accessibility); `DynamoDb` inherits `Gener8.DynamoDbRepository<TModel, TDto>` and takes `IDynamoDbRepositoryContext` (wraps `IDynamoDBContext`); `MongoDb` inherits `Gener8.MongoDbRepository<TModel, TDto>` and takes `IMongoDbRepositoryContext` (wraps `IMongoDatabase`); `Custom` inherits `Gener8.RepositoryBase<TModel, TDto>` (declared `partial`) and takes `IRepositoryContext` (empty interface — consumer provides implementation); context interfaces and base classes are emitted conditionally (once per kind per compilation); SDK base classes override `ToModel`/`ToDto` via generated extension methods; consumer must reference `AWSSDK.DynamoDBv2` or `MongoDB.Driver` for DynamoDb/MongoDb respectively
- Generator targets `netstandard2.0`; uses Roslyn incremental API (`IIncrementalGenerator`)

## Build, packaging, and CI

See [docs/contributing.md](docs/contributing.md) for build commands, NuGet packaging, and CI/CD details.
