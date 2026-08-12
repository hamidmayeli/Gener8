# Gener8

C# source generator that copies public properties from a model class to a decorated partial DTO class.

## Structure

```
FromModel.slnx
src/FromModel/
├── FromModel.csproj        — netstandard2.0, Roslyn packages as private assets
└── FromModelGenerator.cs   — IIncrementalGenerator implementation
```

## How it works

1. `RegisterPostInitializationOutput` injects `FromModelAttribute` into consumer projects — no separate runtime reference needed
2. Consumers decorate a `partial class` with `[FromModel(nameof(TheModel))]`
3. Generator resolves the named type via `Compilation.GetSymbolsWithName`, copies its public instance properties (preserving `get`/`set`/`init`), and emits a `partial class` source file

## Usage

```csharp
public class TheModel { public string Name { get; set; } }

[FromModel(typeof(TheModel))]
internal partial class TheDto {}

// Generator emits:
// internal partial class TheDto { public string Name { get; set; } }
```

## Key details

- Attribute full name: `FromModel.FromModelAttribute`
- Model lookup: resolved directly from the `typeof()` argument — fully-qualified names supported
- Property filter: public, non-static only
- All accessor kinds preserved: `get`, `set`, `init`
- Generator targets `netstandard2.0`; uses Roslyn incremental API (`IIncrementalGenerator`)

## Build

```
dotnet build src/FromModel/FromModel.csproj
dotnet test
```

## NuGet packaging

The generator DLL is placed under `analyzers/dotnet/cs/` in the package (not `lib/`) via `<IncludeBuildOutput>false</IncludeBuildOutput>` and a `<None>` item pointing to `$(OutputPath)$(AssemblyName).dll`. `DevelopmentDependency=true` means consumers automatically get `PrivateAssets=all` — no runtime reference.

Pack locally:
```
dotnet pack src/FromModel/FromModel.csproj -c Release -p:Version=1.2.3
```

## CI / CD

- `.github/workflows/ci.yml` — builds and tests on every push to `main` and on PRs
- `.github/workflows/publish.yml` — manual `workflow_dispatch`; takes a `version` input, builds, tests, packs, pushes to NuGet, and creates a `v{version}` git tag

Uses NuGet Trusted Publishing (OIDC) — no long-lived API key needed. Requires a `NUGET_USERNAME` secret (your nuget.org profile name) and a Trusted Publishing policy configured on nuget.org.
