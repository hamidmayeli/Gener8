# Gener8

C# source generator that copies public properties from a model class to a decorated partial DTO class.

## Structure

```
FromModel.slnx
src/Gener8/
├── Gener8.csproj           — netstandard2.0, Roslyn packages as private assets
├── FromModelGenerator.cs   — IIncrementalGenerator implementation
├── SourceProducer.cs       — Emits the partial class, extension methods, and optional repository
├── SyntaxTransformer.cs    — Roslyn pipeline: predicate, ExtractClassTarget, BuildPropertyData
├── ClassTarget.cs          — record: ClassName, Namespace, Accessibility, Properties, ModelFullName, Repository
├── PropertyData.cs         — record: Type, Name, accessors, ModelPropertyName, FlattenedReadPath, HasTypeMapping
├── RepositoryKind.cs       — internal enum: None, DynamoDb, MongoDb
└── DefaultSource.cs        — injected attribute/enum/base-class source files
```

## How it works

1. `RegisterPostInitializationOutput` injects `FromModelAttribute` (and other attributes) into consumer projects — no separate runtime reference needed
2. Consumers decorate a `partial class` with `[FromModel(typeof(TheModel))]`
3. Generator copies public instance properties (preserving `get`/`set`/`init`, `required`, initializers) and emits up to three files per DTO:
   - `{Namespace}.{ClassName}.g.cs` — the partial class with copied properties
   - `{Namespace}.{ClassName}Extensions.g.cs` — `ToModel` / `ToDto` extension methods
   - `{Namespace}.{ClassName}{DynamoDb|MongoDb}Repository.g.cs` — concrete repository (when `Repository` is set)

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
//     public static TheModel ToModel(this TheDto dto) => new TheModel { Name = dto.Name };
//     public static TheDto   ToDto(this TheModel model) => new TheDto { Name = model.Name };
// }
```

## Key details

- Attribute full name: `Gener8.FromModelAttribute`
- Model lookup: resolved directly from the `typeof()` argument — fully-qualified names supported
- Property filter: public, non-static only
- All accessor kinds preserved: `get`, `set`, `init`
- `[TypeMapping(typeof(A), typeof(ADto))]` — remaps property types; extension methods generate chained `.ToModel()`/`.ToDto()` calls
- `[RenameProperty("OldName", "NewName")]` — renames in DTO; extensions use correct name on each side
- `Flatten = [...]` — inlines nested properties; flattened props appear in `ToDto` (via path) but are skipped in `ToModel`
- Flattened + type-mapped properties are skipped in both extension methods (cannot chain through a flattened path)
- `Repository = RepositoryType.DynamoDb|MongoDb` — generates a concrete repository class inheriting `Gener8.Repository<T>`; consumer must reference `AWSSDK.DynamoDBv2` or `MongoDB.Driver` respectively
- Generator targets `netstandard2.0`; uses Roslyn incremental API (`IIncrementalGenerator`)

## Build

```
dotnet build src/Gener8/Gener8.csproj
dotnet test
```

## NuGet packaging

The generator DLL is placed under `analyzers/dotnet/cs/` in the package (not `lib/`) via `<IncludeBuildOutput>false</IncludeBuildOutput>` and a `<None>` item pointing to `$(OutputPath)$(AssemblyName).dll`. `DevelopmentDependency=true` means consumers automatically get `PrivateAssets=all` — no runtime reference.

Pack locally:
```
dotnet pack src/Gener8/Gener8.csproj -c Release -p:Version=1.2.3
```

## CI / CD

- `.github/workflows/ci.yml` — builds and tests on every push to `main` and on PRs
- `.github/workflows/publish.yml` — manual `workflow_dispatch`; takes a `version` input, builds, tests, packs, pushes to NuGet, and creates a `v{version}` git tag

Uses NuGet Trusted Publishing (OIDC) — no long-lived API key needed. Requires a `NUGET_USERNAME` secret (your nuget.org profile name) and a Trusted Publishing policy configured on nuget.org.
