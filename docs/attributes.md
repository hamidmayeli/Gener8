# Attribute Reference

All attributes are injected by the generator into every consuming project at build time via `RegisterPostInitializationOutput`. You do not need to reference a separate runtime package — they are always available wherever the NuGet package is installed.

---

## `[FromModel]`

**Full name:** `Gener8.FromModelAttribute`  
**Target:** `class`  
**AllowMultiple:** `false`

Marks a `partial class` as a DTO to be populated from the named model type. The class must be declared `partial`.

### Constructor

```csharp
public FromModelAttribute(Type modelType)
```

| Parameter | Type | Description |
|---|---|---|
| `modelType` | `Type` | The source model type. Pass `typeof(MyModel)`. Fully-qualified types are supported. |

### Named properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Ignore` | `string[]` | `[]` | Property names to exclude from the generated output. Use `nameof()` for refactor safety. |
| `IncludeInherited` | `bool` | `false` | When `true`, properties from base classes are also copied. Most-derived property wins when a name is overridden. |
| `Flatten` | `string[]` | `[]` | Property names whose types will be inlined (one level deep) into the DTO instead of being mapped as a whole. |
| `FlattenPrefix` | `FlattenPrefix` | `FlattenPrefix.Parent` | Controls how the parent property name is prepended to flattened property names. |
| `Repository` | `RepositoryType` | `RepositoryType.None` | When set to `DynamoDb`, `MongoDb`, or `Custom`, generates a concrete repository scaffold for the DTO. |
| `DtoNamespaces` | `string[]` | `[]` | Additional namespaces to treat as qualifying for auto type mapping. Types in these namespaces (and the model's own namespace) are automatically remapped to `{TypeName}Dto`, and companion DTOs for those types are auto-generated. |
| `ForceNullable` | `string[]` | `[]` | Property names to make nullable in the DTO even when the model property is non-nullable. |

### Example

```csharp
[FromModel(
    typeof(Order),
    Ignore           = [nameof(Order.InternalNotes)],
    IncludeInherited = true,
    Flatten          = [nameof(Order.BillingAddress)],
    FlattenPrefix    = FlattenPrefix.Gaped,
    Repository       = RepositoryType.DynamoDb,
    DtoNamespaces    = ["MyApp.Models"],
    ForceNullable    = [nameof(Order.Customer)])]
internal partial class OrderDto { }
```

---

## `[TypeMapping]`

**Full name:** `Gener8.TypeMappingAttribute`  
**Target:** `class`  
**AllowMultiple:** `true`

Redirects a property's type in the generated output. When the generator encounters a property whose type matches `SourceType`, it emits `TargetType` instead. Applies to both direct properties and properties introduced by `Flatten`.

### Constructor

```csharp
public TypeMappingAttribute(Type sourceType, Type targetType)
```

| Parameter | Type | Description |
|---|---|---|
| `sourceType` | `Type` | The original property type on the model. |
| `targetType` | `Type` | The replacement type to emit in the DTO. |

### Example

```csharp
[FromModel(typeof(Order))]
[TypeMapping(typeof(Address),  typeof(AddressDto))]
[TypeMapping(typeof(Customer), typeof(CustomerDto))]
internal partial class OrderDto { }
```

---

## `[RenameProperty]`

**Full name:** `Gener8.RenamePropertyAttribute`  
**Target:** `class`  
**AllowMultiple:** `true`

Renames a source property in the generated output. Does not affect properties introduced via `Flatten`.

### Constructor

```csharp
public RenamePropertyAttribute(string sourceName, string targetName)
```

| Parameter | Type | Description |
|---|---|---|
| `sourceName` | `string` | The property name as it appears on the source model. An unknown name is silently ignored. |
| `targetName` | `string` | The name to emit in the generated DTO. |

### Example

```csharp
[FromModel(typeof(Product))]
[RenameProperty(nameof(Product.InternalSku), "Sku")]
[RenameProperty(nameof(Product.DisplayName), "Name")]
internal partial class ProductDto { }
```

---

## `[IgnoreTypeMapping]`

**Full name:** `Gener8.IgnoreTypeMappingAttribute`  
**Target:** `class`  
**AllowMultiple:** `true`

Suppresses automatic type mapping for a specific type when `DtoNamespaces` auto-mapping is active. Use this when a type in a qualifying namespace should not be remapped to a `{TypeName}Dto` and no companion DTO should be auto-generated for it.

### Constructor

```csharp
public IgnoreTypeMappingAttribute(Type ignoredType)
```

| Parameter | Type | Description |
|---|---|---|
| `ignoredType` | `Type` | The type to exclude from auto type mapping. |

### Example

```csharp
[FromModel(typeof(Order), DtoNamespaces = ["MyApp.Models"])]
[IgnoreTypeMapping(typeof(Metadata))]   // Metadata stays as-is; no MetadataDto generated
internal partial class OrderDto { }
```

---

## `FlattenPrefix` enum

**Full name:** `Gener8.FlattenPrefix`

Controls how the parent property name is prepended when a property is flattened.

| Member | Value | Output example (parent: `ShippingAddress`, nested: `City`) |
|---|---|---|
| `Parent` | `0` (default) | `ShippingAddressCity` |
| `None` | `1` | `City` |
| `Gaped` | `2` | `ShippingAddress_City` |

### Example

```csharp
[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)], FlattenPrefix = FlattenPrefix.None)]
internal partial class OrderDto { }
```

---

## `RepositoryType` enum

**Full name:** `Gener8.RepositoryType`

Used with the `Repository` property of `[FromModel]` to opt into repository scaffold generation.

| Member | Value | Description |
|---|---|---|
| `None` | `0` (default) | No repository is generated. |
| `DynamoDb` | `1` | Generates a repository inheriting `Gener8.DynamoDbRepository<TModel, TDto>`. Constructor takes `IDynamoDbRepositoryContext`. Requires `AWSSDK.DynamoDBv2`. |
| `MongoDb` | `2` | Generates a repository inheriting `Gener8.MongoDbRepository<TModel, TDto>`. Constructor takes `IMongoDbRepositoryContext`. Requires `MongoDB.Driver`. |
| `Custom` | `3` | Generates a `partial` repository inheriting `Gener8.RepositoryBase<TModel, TDto>`. Constructor takes `IRepositoryContext` (empty interface). CRUD methods must be added by the consumer in the other partial declaration. |

See [features.md — Repository scaffold](features.md#7-repository-scaffold) for full examples.

---

## `Gener8.IRepository<TModel>` interface

**Full name:** `Gener8.IRepository<TModel>`  
**Constraint:** `TModel : class`  
**Always injected:** yes (via `RegisterPostInitializationOutput`)

Defines the standard CRUD contract. All generated repository classes implement this interface indirectly through their abstract base.

```csharp
public interface IRepository<TModel> where TModel : class
{
    Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task SaveAsync(TModel entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default);
}
```

---

## `Gener8.ICompositeKeyRepository<TModel>` interface

**Full name:** `Gener8.ICompositeKeyRepository<TModel>`  
**Extends:** `IRepository<TModel>`  
**Emitted alongside:** `DynamoDbRepository<TModel, TDto>` (conditionally, when at least one DTO uses `RepositoryType.DynamoDb`)

Extends `IRepository<TModel>` with composite (hash + range) key overloads for DynamoDB tables that use a sort key.

```csharp
public interface ICompositeKeyRepository<TModel> : IRepository<TModel> where TModel : class
{
    Task<TModel?> GetByIdAsync(object hashKey, object rangeKey, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(object hashKey, object rangeKey, CancellationToken cancellationToken = default);
}
```

---

## `Gener8.IDynamoDbRepositoryContext` interface

**Full name:** `Gener8.IDynamoDbRepositoryContext`  
**Emitted alongside:** `DynamoDbRepository<TModel, TDto>` (conditionally)

Wraps an `IDynamoDBContext` so the repository can be constructed without a direct dependency on the AWS SDK type. Consumers implement this interface (or use DI) to supply the underlying context.

```csharp
public interface IDynamoDbRepositoryContext
{
    IDynamoDBContext Context { get; }
}
```

---

## `Gener8.IMongoDbRepositoryContext` interface

**Full name:** `Gener8.IMongoDbRepositoryContext`  
**Emitted alongside:** `MongoDbRepository<TModel, TDto>` (conditionally)

Wraps an `IMongoDatabase` so the repository can be constructed without a direct dependency on the MongoDB.Driver type. Consumers implement this interface (or use DI) to supply the underlying database.

```csharp
public interface IMongoDbRepositoryContext
{
    IMongoDatabase Context { get; }
}
```

---

## `Gener8.DynamoDbRepository<TModel, TDto>` abstract base

**Full name:** `Gener8.DynamoDbRepository<TModel, TDto>`  
**Implements:** `ICompositeKeyRepository<TModel>`  
**Emitted:** conditionally — only when at least one DTO in the compilation sets `Repository = RepositoryType.DynamoDb`

Abstract base for all generated DynamoDB repositories. Accepts an `IDynamoDbRepositoryContext` (which wraps `IDynamoDBContext`) and provides default implementations of every `ICompositeKeyRepository<TModel>` method. Derived classes must implement two abstract methods:

```csharp
protected abstract TModel ToModel(TDto dto);
protected abstract TDto   ToDto(TModel model);
```

The generator overrides both by delegating to the DTO's extension methods (`dto.ToModel()` / `model.ToDto()`).

---

## `Gener8.MongoDbRepository<TModel, TDto>` abstract base

**Full name:** `Gener8.MongoDbRepository<TModel, TDto>`  
**Implements:** `IRepository<TModel>`  
**Emitted:** conditionally — only when at least one DTO in the compilation sets `Repository = RepositoryType.MongoDb`

Abstract base for all generated MongoDB repositories. Accepts an `IMongoDbRepositoryContext` (which wraps `IMongoDatabase`) and a collection name, and provides default implementations of every `IRepository<TModel>` method via `IMongoCollection<TDto>`. Derived classes must implement:

```csharp
protected abstract TModel ToModel(TDto dto);
protected abstract TDto   ToDto(TModel model);
```

The generator overrides both by delegating to the DTO's extension methods.

---

## `Gener8.IRepositoryContext` interface

**Full name:** `Gener8.IRepositoryContext`  
**Emitted alongside:** `RepositoryBase<TModel, TDto>` (conditionally, when at least one DTO uses `RepositoryType.Custom`)

An empty marker interface. Consumers implement it to wrap their own database context or connection and pass it to a `Custom` repository.

```csharp
public interface IRepositoryContext
{
}
```

---

## `Gener8.RepositoryBase<TModel, TDto>` abstract base

**Full name:** `Gener8.RepositoryBase<TModel, TDto>`  
**Implements:** `IRepository<TModel>`  
**Declared:** `abstract partial class` — emitted conditionally when at least one DTO sets `Repository = RepositoryType.Custom`

Abstract base for custom repositories. Accepts an `IRepositoryContext` and provides abstract `ToModel`/`ToDto` hooks. The class is declared `partial`, so consumers can add CRUD method implementations alongside the generated repository in their own partial class declaration.

```csharp
protected abstract TModel ToModel(TDto dto);
protected abstract TDto   ToDto(TModel model);
```

No CRUD methods are pre-implemented — the consumer is responsible for all data access logic.

---

## Property preservation rules

The generator always emits properties as `public`. The following are carried through from the source model unchanged:

| Attribute / modifier | Preserved |
|---|---|
| `set` accessor | Yes |
| `init` accessor | Yes |
| `required` modifier | Yes |
| Property initializer (`= value`) | Yes |
| `get`-only properties (no `set`/`init`) | No — excluded (unless [constructor-backed](features.md#constructor-backed-properties)) |
| `private`, `protected`, `internal` properties | No — excluded |
| `static` properties | No — excluded |
| Indexers | No — excluded |
