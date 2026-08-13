# Contributing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (used by tests and samples; the generator itself targets `netstandard2.0`)
- Any editor with C# support — Visual Studio 2022, VS Code + C# Dev Kit, or Rider

## Repository layout

```
FromModel.slnx
├── src/Gener8/                 — the generator (netstandard2.0)
│   ├── Gener8.csproj
│   ├── FromModelGenerator.cs   — IIncrementalGenerator implementation
│   ├── ClassTarget.cs          — internal record: class metadata for emit
│   ├── PropertyData.cs         — internal record: per-property emit data
│   └── IsExternalInit.cs       — polyfill for init-only setters on netstandard2.0
├── tests/Gener8.Tests/         — xUnit test suite (net10.0)
│   ├── Gener8.Tests.csproj
│   ├── GeneratorDriver.cs      — shared test helper: runs the generator in-memory
│   ├── FromModelGeneratorTests.cs
│   ├── FlattenTests.cs
│   ├── IgnorePropertiesTests.cs
│   ├── IncludeInheritedTests.cs
│   ├── MappingExtensionsTests.cs
│   ├── PropertyModifierTests.cs
│   ├── RenamePropertyTests.cs
│   └── TypeMappingTests.cs
└── samples/Gener8.Sample/      — console app that exercises the generator end-to-end
    ├── Gener8.Sample.csproj
    ├── Product.cs
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

All tests use the shared `GeneratorDriver` helper in `tests/Gener8.Tests/GeneratorDriver.cs`. It:

1. Creates a minimal in-memory `CSharpCompilation` with `mscorlib` and `System.Runtime` references.
2. Runs `CSharpGeneratorDriver.Create(new FromModelGenerator()).RunGeneratorsAndUpdateCompilation(...)`.
3. Asserts the post-generation compilation has **zero diagnostics** (no errors or warnings).
4. Returns `Dictionary<string, string>` mapping hint names to generated source text.

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
