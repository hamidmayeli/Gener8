# Contributing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (used by tests and samples; the generator itself targets `netstandard2.0`)
- Any editor with C# support — Visual Studio 2022, VS Code + C# Dev Kit, or Rider

## Repository layout

```
Gener8.slnx
├── src/Gener8/                     — the generator (netstandard2.0)
│   ├── Gener8.csproj
│   ├── FromModelGenerator.cs       — IIncrementalGenerator implementation
│   ├── SourceProducer.cs           — emits model, extensions, and repository files
│   ├── SyntaxTransformer.cs        — Roslyn pipeline: predicate + ExtractClassTarget
│   ├── PropertyDataBuilder.cs      — builds PropertyData list from a model symbol
│   ├── DefaultSource.cs            — injected attribute/enum/interface source strings
│   ├── IsExternalInit.cs           — polyfill for init-only setters on netstandard2.0
│   ├── Compatibility/
│   │   └── NotNullWhenAttribute.cs — polyfill for [NotNullWhen] on netstandard2.0
│   └── Contexts/                   — immutable records for the incremental pipeline
│       ├── TargetClass.cs          — class metadata for emit
│       ├── ModelClass.cs           — model FullName + simple Name
│       ├── PropertyData.cs         — per-property emit data
│       ├── PropertyTypeData.cs     — type string, HasTypeMapping, NeedsSpreadAssignment
│       ├── FlattenedPropertyData.cs — flatten parent info for ToModel reconstruction
│       ├── FlattenPrefixMode.cs    — enum: Parent / None / Gaped
│       └── RepositoryKind.cs       — enum: None / DynamoDb / MongoDb / Custom
├── tests/Gener8.Tests/             — xUnit unit test suite (net10.0)
│   ├── Gener8.Tests.csproj
│   ├── GeneratorDriver.cs          — shared test helper: Run / RunUnchecked
│   ├── FromModelGeneratorTests.cs
│   ├── FlattenTests.cs
│   ├── IgnorePropertiesTests.cs
│   ├── IncludeInheritedTests.cs
│   ├── MappingExtensionsTests.cs
│   ├── PropertyModifierTests.cs
│   ├── RenamePropertyTests.cs
│   ├── RepositoryTests.cs
│   ├── TypeMappingTests.cs
│   └── UserDeclaredPropertyTests.cs
├── tests/DynamoDb.Integration.Tests/  — DynamoDB integration tests (net10.0, Testcontainers)
│   ├── Setup/Models/               — model + category types
│   ├── Setup/TestFixture.cs        — Testcontainers setup
│   └── TestProductRepository.cs   — integration test class
├── tests/MongoDb.Integration.Tests/   — MongoDB integration tests (net10.0, Testcontainers)
│   ├── Setup/Models/
│   ├── Setup/TestFixture.cs
│   └── TestProductRepository.cs
├── tests/CustomDb.Integration.Tests/  — Custom repository integration tests (net10.0, Testcontainers MsSql)
│   ├── Setup/Models/
│   ├── Setup/ProductDtoRepository.cs  — consumer-supplied repository implementation
│   ├── Setup/TestFixture.cs
│   └── TestProductRepository.cs
└── samples/Gener8.Sample/             — console app that exercises the generator end-to-end
    ├── Gener8.Sample.csproj
    ├── Product.cs
    ├── Repositories.cs
    └── Program.cs
```

## Build

```
dotnet build src/Gener8/Gener8.csproj
```

Build the entire solution (generator + tests + sample):

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

All tests use the shared `GeneratorDriver` helper in `tests/Gener8.Tests/GeneratorDriver.cs`. It provides two overloads:

- **`Run(...)`** — creates a minimal in-memory `CSharpCompilation`, runs the generator, asserts the post-generation compilation has zero errors, and returns `Dictionary<string, string>` mapping hint names to generated source text. Use for all non-repository tests.
- **`RunUnchecked(...)`** — same as `Run` but skips the compilation-error check. Use for repository tests, where the generated base classes reference SDK types (`IDynamoDBContext`, `IMongoDatabase`) that are not in the minimal test compilation. Lets tests assert on generated text without requiring AWS/MongoDB stubs.

A typical test looks like:

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

## NuGet packaging

The project is configured as an **analyzer** package — the generator DLL is placed under `analyzers/dotnet/cs/` rather than `lib/`, and `IncludeBuildOutput=false` keeps it out of the normal `lib/` folder. `DevelopmentDependency=true` means NuGet automatically adds `PrivateAssets=all` for consumers.

Pack a local `.nupkg`:

```
dotnet pack src/Gener8/Gener8.csproj -c Release -p:Version=1.2.3
```

Output goes to `src/Gener8/bin/Release/` by default. Test installing locally:

```
dotnet add package Gener8 --source ./src/Gener8/bin/Release/
```

## CI/CD

### CI workflow (`.github/workflows/ci.yml`)

Runs on every push to `main` and on all pull requests:

1. `dotnet restore`
2. `dotnet build --no-restore -c Release`
3. `dotnet test --no-build -c Release`

### Publish workflow (`.github/workflows/publish.yml`)

Manual dispatch only. Requires a `version` input (e.g. `1.2.3`).

Steps:
1. Restore → build with `-p:Version=${{ inputs.version }}` → test
2. `dotnet pack`
3. Authenticate with NuGet via OIDC (no long-lived API key)
4. `dotnet nuget push` to nuget.org
5. Create and push a `v{version}` git tag

#### NuGet Trusted Publishing setup

The publish workflow uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#trusted-publishing) (OIDC). To configure it:

1. On nuget.org → package → Trusted Publishers → add a GitHub Actions publisher.
2. Add the `NUGET_USERNAME` secret to the GitHub repository (your nuget.org profile name).
3. No long-lived API key is stored anywhere.

## Code style

- `TreatWarningsAsErrors = true` and `Nullable = enable` are enforced.
- `EnforceExtendedAnalyzerRules = true` applies Roslyn-specific analyzer rules to the generator itself.
- Keep the generator code free of external dependencies beyond the Roslyn SDK — both Roslyn packages are `PrivateAssets=all`.
