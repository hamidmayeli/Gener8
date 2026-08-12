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

### ✅ 6. Rename a property

Map a source property to a different name in the generated DTO. Implemented as a separate, repeatable attribute. Renames do not apply to properties introduced via `Flatten`.

```csharp
[FromModel(typeof(Product))]
[RenameProperty(nameof(Product.InternalSku), "Sku")]
[RenameProperty(nameof(Product.DisplayName), "Name")]
internal partial class ProductDto { }
```

---

### 7. Force nullability

Override accessor nullability for all copied properties — useful when building DTOs that represent optional/partial payloads (e.g., PATCH request bodies).

```csharp
[FromModel(typeof(Product), MakeAllNullable = true)]
internal partial class ProductPatchDto { }

// Emits: public string? Name { get; set; }  (even if model has non-nullable string)
```

---

### 8. Override accessors

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
| 6 | Rename property | Low | Medium | ✅ Done |
| 7 | Force nullability | Low | Medium | |
| 8 | Override accessors | Low | Medium | |
| 4 | Flatten nested | Medium | Medium | ✅ Done |
| 5 | Multiple source models | Medium | Low | |
