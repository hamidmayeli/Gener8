# FromModel — Feature Roadmap

### 1. Ignore properties

Exclude specific model properties from the generated DTO.

**Proposed syntax — attribute parameter (keeps DTO concerns on the DTO side):**

```csharp
[FromModel(typeof(Product), Ignore = [nameof(Product.InternalCode), nameof(Product.AuditTimestamp)])]
internal partial class ProductDto { }
```

Alternative — attribute on the model property itself, but this couples the model to DTO generation concerns and is less desirable.

---

### 2. Nested complex type mapping

When a model property's type is itself a model that has a corresponding DTO, use the DTO type instead of the original model type in the generated output.

**Syntax — separate repeatable `TypeMappingAttribute`:**

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

Each `[TypeMapping(source, target)]` redirects one type. Multiple attributes are stacked for multiple mappings. Explicit mapping is preferred over automatic inference (scanning all `[FromModel]`-decorated classes in the compilation) because it is predictable and avoids hidden cross-DTO coupling.

---

### 3. Include inherited properties

By default the generator only copies properties declared directly on the model type. An opt-in flag would also walk the inheritance chain.

```csharp
[FromModel(typeof(DerivedProduct), IncludeInherited = true)]
internal partial class DerivedProductDto { }
```

---

### 4. Flatten a nested object

Instead of mapping a nested type to a corresponding DTO, copy its properties directly into the parent DTO (one level deep).

```csharp
// Order.ShippingAddress is of type Address { Street, City, PostCode }
[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
internal partial class OrderDto { }

// Generator emits Street, City, PostCode directly on OrderDto (no AddressDto)
```

Useful when the DTO represents a denormalised view (e.g., a read model or a response payload).

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

### 6. Rename a property

Map a source property to a different name in the generated DTO, covering cases where the model name is domain-internal and the DTO name is public/API-facing.

```csharp
[FromModel(typeof(Product))]
[FromModelRename(nameof(Product.InternalSku), "Sku")]
[FromModelRename(nameof(Product.DisplayName), "Name")]
internal partial class ProductDto { }
```

Implemented as a separate, repeatable attribute rather than a parameter to keep the primary attribute readable.

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

| # | Feature | Complexity | Value |
|---|---------|-----------|-------|
| 1 | Ignore properties | Low | High |
| 2 | Nested type mapping | Medium | High |
| 3 | Include inherited | Low | Medium |
| 6 | Rename property | Low | Medium |
| 7 | Force nullability | Low | Medium |
| 8 | Override accessors | Low | Medium |
| 4 | Flatten nested | Medium | Medium |
| 5 | Multiple source models | Medium | Low |
