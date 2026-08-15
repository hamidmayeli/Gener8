# Gener8 — Feature Roadmap

### ✅ 1. Ignore properties

Exclude specific model properties from the generated DTO.

```csharp
[FromModel(typeof(Product), Ignore = [nameof(Product.InternalCode), nameof(Product.AuditTimestamp)])]
internal partial class ProductDto { }
```

---

### ✅ 2. Nested complex type mapping

When a model property's type is itself a model that has a corresponding DTO, use the DTO type instead of the original model type in the generated output.

```csharp
[FromModel(typeof(Order))]
[TypeMapping(typeof(Address), typeof(AddressDto))]
internal partial class OrderDto { }

[FromModel(typeof(Address))]
internal partial class AddressDto { }

// Generator emits in OrderDto:
// public AddressDto ShippingAddress { get; set; }
// public AddressDto BillingAddress { get; set; }
```

Each `[TypeMapping(source, target)]` redirects one type. Multiple attributes are stacked for multiple mappings.

---

### ✅ 3. Include inherited properties

By default the generator only copies properties declared directly on the model type. An opt-in flag also walks the inheritance chain. Most-derived property wins when overriding.

```csharp
[FromModel(typeof(DerivedProduct), IncludeInherited = true)]
internal partial class DerivedProductDto { }
```

---

### ✅ 4. Flatten a nested object

Instead of mapping a nested type to a corresponding DTO, copy its properties directly into the parent DTO (one level deep). `TypeMapping` still applies to the flattened properties' types. A property in both `Ignore` and `Flatten` is dropped, not flattened.

An optional `FlattenPrefix` controls how the parent property name is prepended to each nested property name:

| Value | Example output |
|-------|---------------|
| `Parent` (default) | `ShippingAddressCity` |
| `None` | `City` |
| `Gaped` | `ShippingAddress_City` |

```csharp
// Order.ShippingAddress is of type Address { Street, City, PostCode }
[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
internal partial class OrderDto { }

// Generator emits: ShippingAddressStreet, ShippingAddressCity, ShippingAddressPostCode
```

---

### 5. Compose from multiple models

Allow a single DTO to merge properties from more than one source model, which requires changing `AllowMultiple = true` on the attribute.

```csharp
[FromModel(typeof(Product))]
[FromModel(typeof(Pricing))]
internal partial class ProductSummaryDto { }
```

Name conflicts should produce a diagnostic rather than silently dropping one of the properties.

---

### ✅ 6. Mapping extension methods

Alongside every generated DTO, the generator emits a `{DtoName}Extensions` static class with two extension methods:

- `ToModel(this DtoType dto)` — maps the DTO back to the model
- `ToDto(this ModelType model)` — maps the model to the DTO

Type-mapped properties generate chained calls (`dto.Address.ToModel()` / `model.Address.ToDto()`). Renamed properties use the correct side in each direction. Flattened properties are included in `ToDto` via their nested read path and skipped in `ToModel`.

```csharp
[FromModel(typeof(Product))]
internal partial class ProductDto { }

// Generated:
// public static Product ToModel(this ProductDto dto) => new Product { Name = dto.Name, ... };
// public static ProductDto ToDto(this Product model) => new ProductDto { Name = model.Name, ... };
```

---

### ✅ 7. Rename a property

Map a source property to a different name in the generated DTO. Implemented as a separate, repeatable attribute. Renames do not apply to properties introduced via `Flatten`.

```csharp
[FromModel(typeof(Product))]
[RenameProperty(nameof(Product.InternalSku), "Sku")]
[RenameProperty(nameof(Product.DisplayName), "Name")]
internal partial class ProductDto { }
```

---

### ✅ 8. Repository scaffold

Opt into generating a concrete repository class by setting `Repository` on `[FromModel]`. Three options are available: `RepositoryType.DynamoDb`, `RepositoryType.MongoDb`, and `RepositoryType.Custom`.

```csharp
[FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
internal partial class ProductDto { }

// Generates ProductDtoRepository : Gener8.DynamoDbRepository<Product, ProductDto>
// with constructor (IDynamoDbRepositoryContext context)
// and ToModel/ToDto overrides wired to the generated extension methods.
```

```csharp
[FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
internal partial class ProductDto { }

// Generates ProductDtoRepository : Gener8.MongoDbRepository<Product, ProductDto>
// with constructor (IMongoDbRepositoryContext context)
// using "ProductDto" as the collection name.
```

```csharp
[FromModel(typeof(Product), Repository = RepositoryType.Custom)]
internal partial class ProductDto { }

// Generates ProductDtoRepository : Gener8.RepositoryBase<Product, ProductDto>
// with constructor (IRepositoryContext context) — IRepositoryContext is an empty
// marker interface; implement it to wrap your own DB context.
// The class is partial — add CRUD methods in a second partial declaration.
```

`DynamoDbRepository<TModel, TDto>` implements `ICompositeKeyRepository<TModel>` (full CRUD + composite-key overloads). `MongoDbRepository<TModel, TDto>` implements `IRepository<TModel>`. `RepositoryBase<TModel, TDto>` is a `partial` abstract base — no CRUD pre-implemented. All abstract base classes are emitted only when at least one DTO requests them.

Requires `AWSSDK.DynamoDBv2` or `MongoDB.Driver` in the consuming project for DynamoDb/MongoDb respectively. `Custom` has no additional dependency.

---

### 10. Force nullability

Override accessor nullability for all copied properties — useful when building DTOs that represent optional/partial payloads (e.g., PATCH request bodies).

```csharp
[FromModel(typeof(Product), MakeAllNullable = true)]
internal partial class ProductPatchDto { }

// Emits: public string? Name { get; set; }  (even if model has non-nullable string)
```

---

### 11. Override accessors

Emit properties with a different accessor pattern than the source — for instance, force all properties to `init`-only in an immutable response DTO.

```csharp
[FromModel(typeof(Product), Accessors = PropertyAccessors.GetInit)]
internal partial class ProductResponseDto { }
```

Possible values: `Preserve` (default), `GetOnly`, `GetSet`, `GetInit`.

---

## Priority suggestion

| # | Feature | Complexity | Value | Status |
|---|---------|-----------|-------|--------|
| 1 | Ignore properties | Low | High | ✅ Done |
| 2 | Nested type mapping | Medium | High | ✅ Done |
| 3 | Include inherited | Low | Medium | ✅ Done |
| 4 | Flatten nested | Medium | Medium | ✅ Done |
| 5 | Multiple source models | Medium | Low | |
| 6 | Rename property | Low | Medium | ✅ Done |
| 7 | Mapping extension methods | Medium | High | ✅ Done |
| 8 | Repository scaffold | Medium | High | ✅ Done |
| 9 | Force nullability | Low | Medium | |
| 10 | Override accessors | Low | Medium | |
