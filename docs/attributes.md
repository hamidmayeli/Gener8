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

### Example

```csharp
[FromModel(
    typeof(Order),
    Ignore           = [nameof(Order.InternalNotes)],
    IncludeInherited = true,
    Flatten          = [nameof(Order.BillingAddress)],
    FlattenPrefix    = FlattenPrefix.Gaped)]
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

## Property preservation rules

The generator always emits properties as `public`. The following are carried through from the source model unchanged:

| Attribute / modifier | Preserved |
|---|---|
| `get` accessor | Yes |
| `set` accessor | Yes |
| `init` accessor | Yes |
| `required` modifier | Yes |
| Property initializer (`= value`) | Yes |
| `private`, `protected`, `internal` properties | No — excluded |
| `static` properties | No — excluded |
| Indexers | No — excluded |
