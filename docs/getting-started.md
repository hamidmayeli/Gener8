# Getting Started

## Requirements

- .NET SDK 6 or later
- C# 9 or later

## Installation

### Basic setup

```
dotnet add package Gener8
```

This single package gives you everything you need: the Roslyn source generator and all core types (`[FromModel]`, `[TypeMapping]`, `RepositoryType`, etc.).

### DynamoDB repository support

```
dotnet add package Gener8
dotnet add package Gener8.Extensions.DynamoDB
```

`Gener8.Extensions.DynamoDB` provides the `DynamoDbRepository<TModel, TDto>` abstract base class and the DynamoDB enum converters. You still need `AWSSDK.DynamoDBv2` referenced in your project for the full SDK.

### MongoDB repository support

```
dotnet add package Gener8
dotnet add package Gener8.Extensions.MongoDB
```

`Gener8.Extensions.MongoDB` provides `MongoDbRepository<TModel, TDto>`. You still need `MongoDB.Driver` referenced in your project.

### Custom repository support

```
dotnet add package Gener8
```

No extra package needed. `RepositoryBase<TModel, TDto>` and `IRepositoryContext` are included in the core `Gener8` package (via the bundled `Gener8.Abstractions.dll`).

---

## Your first DTO

### 1. Define your model

```csharp
public class Product
{
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
}
```

### 2. Declare a partial DTO class with `[FromModel]`

```csharp
using Gener8;

[FromModel(typeof(Product))]
internal partial class ProductDto { }
```

### 3. Build

The generator runs automatically on every build. All properties are copied:

```csharp
// Generated: ProductDto.g.cs
internal partial class ProductDto
{
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
}
```

Extension methods are generated too:

```csharp
// Generated: ProductDtoExtensions.g.cs
internal static partial class ProductDtoExtensions
{
    public static Product?    ToModel(this ProductDto? dto)   => ...;
    public static ProductDto? ToDto  (this Product?    model) => ...;
}
```

## Viewing generated files

To inspect the generated source, add `EmitCompilerGeneratedFiles` to your project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

Generated files appear under `obj/Debug/netX.Y/generated/Gener8/Gener8.FromModelGenerator/`.

## Namespace behaviour

- The DTO and its extension class are emitted in the same namespace as the partial class declaration.
- If the partial class has no namespace (global namespace), the generated files have no namespace either.

## Next steps

- [Features](features.md) — all features with code examples
- [Attribute reference](attributes.md) — complete API reference
- [How it works](how-it-works.md) — generator internals
