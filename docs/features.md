# Features

## Property copying — what is preserved

By default, `[FromModel]` copies every **public, non-static instance property** declared directly on the model type. For each property the generator preserves:

- The fully-qualified type name (including nullability annotations)
- The property name
- All accessor kinds: `get`, `set`, `init`
- The `required` modifier
- Property initializers (`= ""`, `= 42`, `= string.Empty`, etc.)

```csharp
public class Product
{
    public required string Name    { get; set; }        // required preserved
    public string Description      { get; set; } = "";  // initializer preserved
    public decimal Price           { get; set; }
    public string Slug             { get; init; }       // init preserved
    public string? Tag             { get; }             // get-only preserved
}

[FromModel(typeof(Product))]
internal partial class ProductDto { }

// Generated:
// public required string Name    { get; set; }
// public string Description      { get; set; } = "";
// public decimal Price           { get; set; }
// public string Slug             { get; init; }
// public string? Tag             { get; }
```

---

## 1. Ignore properties

Exclude one or more source properties from the generated DTO by name.

```csharp
[FromModel(typeof(Product), Ignore = [nameof(Product.InternalCode), nameof(Product.AuditTimestamp)])]
internal partial class ProductDto { }
```

- Uses `string[]` — pass property names as `nameof(...)` expressions to stay refactor-safe.
- A name that does not exist on the model is silently ignored.
- A property listed in both `Ignore` and `Flatten` is dropped (not flattened).

---

## 2. Type mapping

When a model property's type is itself a domain object that has a DTO, redirect the generated type to the DTO type instead.

```csharp
public class Order
{
    public int Id { get; set; }
    public Address ShippingAddress { get; set; }
    public Address BillingAddress  { get; set; }
}

[FromModel(typeof(Order))]
[TypeMapping(typeof(Address), typeof(AddressDto))]
internal partial class OrderDto { }

[FromModel(typeof(Address))]
internal partial class AddressDto { }
```

Generated `OrderDto`:

```csharp
public int        Id              { get; set; }
public AddressDto ShippingAddress { get; set; }
public AddressDto BillingAddress  { get; set; }
```

**Multiple mappings** — stack `[TypeMapping]` attributes, one per type pair:

```csharp
[FromModel(typeof(Order))]
[TypeMapping(typeof(Address),  typeof(AddressDto))]
[TypeMapping(typeof(Customer), typeof(CustomerDto))]
internal partial class OrderDto { }
```

Type mapping also applies to properties introduced via `Flatten`.

---

## 3. Include inherited properties

By default only properties declared directly on the model class are copied. Set `IncludeInherited = true` to walk the full inheritance chain.

```csharp
public class BaseEntity
{
    public int    Id        { get; set; }
    public string CreatedBy { get; set; }
}

public class Product : BaseEntity
{
    public string Name  { get; set; }
    public decimal Price { get; set; }
}

[FromModel(typeof(Product), IncludeInherited = true)]
internal partial class ProductDto { }

// Generated: Name, Price, Id, CreatedBy
```

**Override deduplication** — when a derived class overrides a property from a base class, only the most-derived version is emitted. The traversal stops at `System.Object`.

---

## 4. Flatten a nested object

Instead of mapping a nested type to a corresponding DTO type, inline its properties directly into the parent DTO. Flattening is one level deep.

```csharp
public class Address { public string Street { get; set; } public string City { get; set; } public string PostCode { get; set; } }
public class Order   { public int Id { get; set; } public Address ShippingAddress { get; set; } }

[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
internal partial class OrderDto { }
```

Generated `OrderDto` (default `FlattenPrefix.Parent`):

```csharp
public int    Id                    { get; set; }
public string ShippingAddressStreet   { get; set; }
public string ShippingAddressCity     { get; set; }
public string ShippingAddressPostCode { get; set; }
```

### FlattenPrefix

Control how the parent property name is prepended to each nested property name:

| Value | Example output | Notes |
|---|---|---|
| `FlattenPrefix.Parent` (default) | `ShippingAddressCity` | PascalCase concatenation |
| `FlattenPrefix.None` | `City` | No prefix; beware of name collisions |
| `FlattenPrefix.Gaped` | `ShippingAddress_City` | Underscore separator |

```csharp
[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)], FlattenPrefix = FlattenPrefix.Gaped)]
internal partial class OrderDto { }
// Emits: ShippingAddress_Street, ShippingAddress_City, ShippingAddress_PostCode
```

**Interaction rules**
- A property listed in both `Flatten` and `Ignore` is dropped.
- `TypeMapping` applies to the types of properties introduced through flattening.
- `RenameProperty` does **not** apply to flattened properties (use `nameOverride` patterns instead).

---

## 5. Mapping extension methods

For every DTO, the generator automatically emits a companion static class `{DtoName}Extensions` with two extension methods:

- `ToModel(this DtoType dto)` — maps the DTO back to the original model type
- `ToDto(this ModelType model)` — maps the model to the DTO type

```csharp
public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } }

[FromModel(typeof(Product))]
internal partial class ProductDto { }

// Generated ProductDtoExtensions:
// internal static class ProductDtoExtensions
// {
//     public static Product ToModel(this ProductDto dto) => new Product { Name = dto.Name, Price = dto.Price };
//     public static ProductDto ToDto(this Product model) => new ProductDto { Name = model.Name, Price = model.Price };
// }
```

The extension class accessibility mirrors the DTO: `public` DTOs get `public` extensions, everything else gets `internal`.

**With `[TypeMapping]`** — mapped properties generate chained calls rather than direct assignment. Null-conditional `?.` is used automatically for nullable mapped properties:

```csharp
[FromModel(typeof(Order))]
[TypeMapping(typeof(Address), typeof(AddressDto))]
internal partial class OrderDto { }

// ToModel:  ShippingAddress = dto.ShippingAddress.ToModel(),
// ToDto:    ShippingAddress = model.ShippingAddress.ToDto(),
```

**With `[RenameProperty]`** — the correct side is used in each method:

```csharp
[FromModel(typeof(Product))]
[RenameProperty(nameof(Product.InternalSku), "Sku")]
internal partial class ProductDto { }

// ToModel:  InternalSku = dto.Sku,
// ToDto:    Sku = model.InternalSku,
```

**With `Flatten`** — flattened properties appear in `ToDto` via their nested path; `ToModel` reconstructs the nested parent object inline from the spread DTO properties:

```csharp
[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
internal partial class OrderDto { }

// ToDto  includes: ShippingAddressStreet = model.ShippingAddress.Street,
// ToModel reconstructs:
//   ShippingAddress = new global::Address { Street = dto.ShippingAddressStreet, ... },
// For nullable parents (Address?):
//   ShippingAddress = dto.ShippingAddressStreet is null ? null : new global::Address { Street = dto.ShippingAddressStreet!, ... },
```

---

## 6. Rename a property

Map a source property to a different name in the generated output. Apply `[RenameProperty]` once per rename — the attribute is repeatable.

```csharp
[FromModel(typeof(Product))]
[RenameProperty(nameof(Product.InternalSku),  "Sku")]
[RenameProperty(nameof(Product.DisplayName),  "Name")]
internal partial class ProductDto { }
```

- An unknown source name is silently ignored.
- Renames do **not** apply to properties introduced via `Flatten`.

---

## Combining features

Features compose freely. A typical real-world DTO might use several at once:

```csharp
[FromModel(typeof(Order), Ignore = [nameof(Order.InternalNotes)], IncludeInherited = true)]
[TypeMapping(typeof(Address),  typeof(AddressDto))]
[TypeMapping(typeof(Customer), typeof(CustomerSummaryDto))]
[RenameProperty(nameof(Order.ExternalRef), "Reference")]
internal partial class OrderResponseDto { }
```

The companion `OrderResponseDtoExtensions` class is generated automatically alongside the DTO, with full awareness of all the above mappings and renames.

---

## 7. Repository scaffold

Set `Repository = RepositoryType.DynamoDb` or `Repository = RepositoryType.MongoDb` on `[FromModel]` to have the generator emit a concrete repository class alongside the DTO. The generated class inherits from the SDK-specific abstract base provided by Gener8, and `ToModel`/`ToDto` overrides are wired automatically to the generated extension methods.

### DynamoDB

```csharp
[FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
internal partial class ProductDto { }
```

Generated `ProductDtoRepository.g.cs`:

```csharp
// <auto-generated/>
#nullable enable

internal partial class ProductDtoRepository : Gener8.DynamoDbRepository<Product, ProductDto>
{
    public ProductDtoRepository(IDynamoDbRepositoryContext context) : base(context) {}
    protected override Product    ToModel(ProductDto dto)   => dto.ToModel();
    protected override ProductDto ToDto  (Product    model) => model.ToDto();
}
```

The constructor accepts an `IDynamoDbRepositoryContext` (which wraps `IDynamoDBContext`) and delegates all CRUD operations to the `DynamoDbRepository<TModel, TDto>` base class. The base also implements `ICompositeKeyRepository<TModel>`, which extends `IRepository<TModel>` with two-key `GetByIdAsync` and `DeleteByIdAsync` overloads.

Requires the `AWSSDK.DynamoDBv2` NuGet package.

### MongoDB

```csharp
[FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
internal partial class ProductDto { }
```

Generated `ProductDtoRepository.g.cs`:

```csharp
// <auto-generated/>
#nullable enable

internal partial class ProductDtoRepository : Gener8.MongoDbRepository<Product, ProductDto>
{
    public ProductDtoRepository(IMongoDbRepositoryContext context) : base(context, "ProductDto") {}
    protected override Product    ToModel(ProductDto dto)   => dto.ToModel();
    protected override ProductDto ToDto  (Product    model) => model.ToDto();
}
```

The constructor accepts an `IMongoDbRepositoryContext` (which wraps `IMongoDatabase`) and uses the DTO class name as the collection name. The base class implements `IRepository<TModel>` and resolves the `IMongoCollection<TDto>` internally.

Requires the `MongoDB.Driver` NuGet package.

### Custom

Use `RepositoryType.Custom` to get a repository scaffold without any built-in SDK dependency. The generated class is declared `partial`, so you add CRUD implementations in your own partial declaration alongside the generated file.

```csharp
[FromModel(typeof(Product), Repository = RepositoryType.Custom)]
internal partial class ProductDto { }
```

Generated `ProductDtoRepository.g.cs`:

```csharp
// <auto-generated/>
#nullable enable

internal partial class ProductDtoRepository : Gener8.RepositoryBase<Product, ProductDto>
{
    public ProductDtoRepository(IRepositoryContext context) : base(context) {}
    protected override Product    ToModel(ProductDto dto)   => dto.ToModel();
    protected override ProductDto ToDto  (Product    model) => model.ToDto();
}
```

The constructor accepts an `IRepositoryContext` (an empty marker interface — wrap your own DB context in it). No CRUD methods are generated; implement them in a second partial class file.

No additional NuGet package is required.

### Abstract bases and interfaces

The abstract base classes (`DynamoDbRepository<TModel, TDto>` and `MongoDbRepository<TModel, TDto>`) are emitted into the compilation **only when at least one DTO requests them** — they are never injected unconditionally so no SDK-type references leak into projects that do not use repositories. The context interfaces (`IDynamoDbRepositoryContext` and `IMongoDbRepositoryContext`) are emitted alongside their respective base classes.

The `IRepository<TModel>` interface (always injected) defines the standard CRUD contract:

| Method | Signature |
|---|---|
| `GetByIdAsync` | `Task<TModel?> GetByIdAsync(object id, CancellationToken ct = default)` |
| `GetAllAsync` | `Task<IEnumerable<TModel>> GetAllAsync(CancellationToken ct = default)` |
| `SaveAsync` | `Task SaveAsync(TModel entity, CancellationToken ct = default)` |
| `DeleteAsync` | `Task DeleteAsync(TModel entity, CancellationToken ct = default)` |
| `DeleteByIdAsync` | `Task DeleteByIdAsync(object id, CancellationToken ct = default)` |

`ICompositeKeyRepository<TModel>` (emitted alongside `DynamoDbRepository`) extends `IRepository<TModel>` with:

| Method | Signature |
|---|---|
| `GetByIdAsync` | `Task<TModel?> GetByIdAsync(object hashKey, object rangeKey, CancellationToken ct = default)` |
| `DeleteByIdAsync` | `Task DeleteByIdAsync(object hashKey, object rangeKey, CancellationToken ct = default)` |

---

## Planned features

The following features are on the roadmap but not yet implemented:

| Feature | Description |
|---|---|
| **Compose from multiple models** | Merge properties from more than one source model into a single DTO |
| **Force nullability** | Emit all properties as nullable (`string?`) regardless of source nullability |
| **Override accessors** | Emit all properties with a specific accessor pattern (`GetOnly`, `GetSet`, `GetInit`) |
