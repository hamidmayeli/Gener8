# Contributing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (used by tests and samples; the generator itself targets `netstandard2.0`)
- Any editor with C# support — Visual Studio 2022, VS Code + C# Dev Kit, or Rider

## Repository layout

```
Gener8.slnx
├── src/
│   ├── Gener8.Abstractions/            — core attributes, enums, and repository contracts (netstandard2.0, zero deps)
│   │   ├── Gener8.Abstractions.csproj
│   │   ├── FromModelAttribute.cs
│   │   ├── TypeMappingAttribute.cs
│   │   ├── RenamePropertyAttribute.cs
│   │   ├── IgnoreTypeMappingAttribute.cs
│   │   ├── RepositoryType.cs
│   │   ├── FlattenPrefix.cs
│   │   ├── IRepository.cs
│   │   ├── ICompositeKeyRepository.cs
│   │   ├── IRepositoryContext.cs
│   │   └── RepositoryBase.cs
│   ├── Gener8/                         — the Roslyn source generator (netstandard2.0)
│   │   ├── Gener8.csproj
│   │   ├── FromModelGenerator.cs       — IIncrementalGenerator implementation
│   │   ├── SourceProducer.cs           — emits model, extensions, and repository files
│   │   ├── SyntaxTransformer.cs        — Roslyn pipeline: predicate + ExtractClassTarget
│   │   ├── PropertyDataBuilder.cs      — builds PropertyData list from a model symbol
│   │   ├── DefaultSource.cs            — placeholder (all types moved to Abstractions/Extensions)
│   │   ├── DefaultSource.DynamoDb.cs   — placeholder (moved to Extensions.DynamoDB)
│   │   ├── IsExternalInit.cs           — polyfill for init-only setters on netstandard2.0
│   │   ├── Compatibility/
│   │   │   └── NotNullWhenAttribute.cs — polyfill for [NotNullWhen] on netstandard2.0
│   │   └── Contexts/                   — immutable records for the incremental pipeline
│   │       ├── TargetClass.cs
│   │       ├── ModelClass.cs
│   │       ├── PropertyData.cs
│   │       ├── PropertyTypeData.cs
│   │       ├── FlattenedPropertyData.cs
│   │       ├── FlattenPrefixMode.cs
│   │       └── RepositoryKind.cs
│   ├── Gener8.Extensions.DynamoDB/     — DynamoDB integration (netstandard2.0, requires AWSSDK.DynamoDBv2)
│   │   ├── Gener8.Extensions.DynamoDB.csproj
│   │   ├── Converters/
│   │   │   ├── EnumToStringConverter.cs
│   │   │   ├── NullableEnumToStringConverter.cs
│   │   │   ├── EnumListToStringListConverter.cs
│   │   │   └── NullableEnumListToStringListConverter.cs
│   │   └── Repositories/
│   │       ├── IDynamoDbRepositoryContext.cs
│   │       └── DynamoDbRepository.cs
│   └── Gener8.Extensions.MongoDB/      — MongoDB integration (netstandard2.0, requires MongoDB.Driver)
│       ├── Gener8.Extensions.MongoDB.csproj
│       └── Repositories/
│           ├── IMongoDbRepositoryContext.cs
│           └── MongoDbRepository.cs
├── tests/Gener8.Tests/             — xUnit unit test suite (net10.0)
│   ├── Gener8.Tests.csproj
│   ├── GeneratorDriver.cs          — shared test helper: Run / RunWithNullable / RunUnchecked / RunForDiagnostics
│   └── ...
├── tests/DynamoDb.Integration.Tests/  — DynamoDB integration tests (net10.0, Testcontainers)
├── tests/MongoDb.Integration.Tests/   — MongoDB integration tests (net10.0, Testcontainers)
├── tests/CustomDb.Integration.Tests/  — Custom repository integration tests (net10.0, Testcontainers MsSql)
└── samples/Gener8.Sample/             — console app that exercises the generator end-to-end
```

## Build

Build the generator only:

```
dotnet build src/Gener8/Gener8.csproj
```

Build the entire solution (all projects):

```
dotnet build
```

## Run the tests

```
dotnet test
```

Run a specific test class:

```
dotnet test --filter "FullyQualifiedName~FlattenTests"
```

### Writing tests

All unit tests use the shared `GeneratorDriver` helper in `tests/Gener8.Tests/GeneratorDriver.cs`. It provides these overloads:

- **`Run(...)`** — creates a minimal in-memory `CSharpCompilation` (with `Gener8.Abstractions.dll` included as a metadata reference), runs the generator, asserts the post-generation compilation has zero errors, and returns `Dictionary<string, string>` mapping hint names to generated source text. Use for all non-repository tests.
- **`RunWithNullable(...)`** — same as `Run` but enables nullable context.
- **`RunUnchecked(...)`** — skips the compilation-error check. Use for repository tests where the generated concrete class references SDK types (`IDynamoDbRepositoryContext`, `IMongoDbRepositoryContext`) that are not in the minimal test compilation.
- **`RunForDiagnostics(...)`** — returns generator diagnostics (GENxxx) without asserting on generated source.

A typical test:

```csharp
[Fact]
public void CopiesPublicProperty()
{
    var sources = new Dictionary<string, string>
    {
        ["Model.cs"] = "public class MyModel { public string Name { get; set; } }",
        ["Dto.cs"]   = """
            using Gener8;
            [FromModel(typeof(MyModel))]
            internal partial class MyDto { }
            """,
    };

    var generated = GeneratorDriver.Run(sources);

    Assert.Contains("public string Name { get; set; }", generated["MyDto.g.cs"]);
}
```

> **Note**: `[FromModel]` and other Gener8 attributes are no longer injected by the generator at test runtime — they come from `Gener8.Abstractions.dll`, which `GeneratorDriver.CreateCompilation` includes automatically via `typeof(FromModelAttribute).Assembly.Location`.

## NuGet packages

The project publishes three NuGet packages:

| Package | Contents | Consumer adds |
|---|---|---|
| `Gener8` | Generator DLL (`analyzers/dotnet/cs/`) + Abstractions DLL (`lib/netstandard2.0/`) | Always (core requirement) |
| `Gener8.Extensions.DynamoDB` | DynamoDB converters + `DynamoDbRepository<TModel,TDto>` | When using `RepositoryType.DynamoDb` |
| `Gener8.Extensions.MongoDB` | `MongoDbRepository<TModel,TDto>` | When using `RepositoryType.MongoDb` |

`Gener8.Abstractions` is also published as a standalone package for consumers who want to reference it independently (e.g., in a shared contracts project).

### Packing locally

```
dotnet pack src/Gener8.Abstractions/Gener8.Abstractions.csproj -c Release -p:Version=1.2.3
dotnet pack src/Gener8/Gener8.csproj -c Release -p:Version=1.2.3
dotnet pack src/Gener8.Extensions.DynamoDB/Gener8.Extensions.DynamoDB.csproj -c Release -p:Version=1.2.3
dotnet pack src/Gener8.Extensions.MongoDB/Gener8.Extensions.MongoDB.csproj -c Release -p:Version=1.2.3
```

Output goes to each project's `bin/Release/` by default. Test installing locally:

```
dotnet add package Gener8 --source ./src/Gener8/bin/Release/
```

### How Gener8 bundles Abstractions

`Gener8.csproj` includes `Gener8.Abstractions.dll` in `lib/netstandard2.0/` via a `<None Pack="true">` item. The generator DLL is placed under `analyzers/dotnet/cs/` as before. This means a single `dotnet add package Gener8` gives consumers both the generator and the attribute/enum/interface DLL — no separate step needed.

## CI/CD

### CI workflow (`.github/workflows/ci.yml`)

Runs on every push to `main` and on all pull requests:

1. `dotnet restore`
2. `dotnet build --no-restore -c Release`
3. Four parallel test suites (unit, DynamoDB, MongoDB, CustomDb) — all must pass

### Publish workflow (`.github/workflows/publish.yml`)

Manual dispatch only. Requires a `version` input (e.g. `1.2.3`).

Steps:
1. Restore → build with `-p:Version=${{ inputs.version }}` → test
2. Pack all four packages (`Gener8.Abstractions`, `Gener8`, `Gener8.Extensions.DynamoDB`, `Gener8.Extensions.MongoDB`) into `./nupkg/`
3. Authenticate with NuGet via OIDC (no long-lived API key)
4. `dotnet nuget push ./nupkg/*.nupkg` — pushes all packages in one step
5. Create and push a `v{version}` git tag

#### NuGet Trusted Publishing setup

The publish workflow uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#trusted-publishing) (OIDC). To configure it:

1. On nuget.org → each package → Trusted Publishers → add a GitHub Actions publisher.
2. Add the `NUGET_USERNAME` secret to the GitHub repository (your nuget.org profile name).
3. No long-lived API key is stored anywhere.

## Code style

- `TreatWarningsAsErrors = true` and `Nullable = enable` are enforced via `Directory.Build.props`.
- `EnforceExtendedAnalyzerRules = true` applies Roslyn-specific analyzer rules to the generator.
- Keep the generator code free of external dependencies beyond the Roslyn SDK — both Roslyn packages are `PrivateAssets=all`.
- `Gener8.Abstractions` must have zero third-party dependencies.
