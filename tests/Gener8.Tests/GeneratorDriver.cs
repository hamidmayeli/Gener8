using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace Gener8.Tests;

internal static class GeneratorDriver
{
    /// <summary>
    /// Compiles <paramref name="sources"/>, runs <see cref="FromModelGenerator"/>, and returns
    /// every source file the generator emitted (keyed by hint name).
    /// Throws if the post-generation compilation has errors.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Run(params string[] sources)
        => RunCore(NullableContextOptions.Disable, sources);

    /// <summary>
    /// Compiles <paramref name="sources"/> with nullable enabled, runs <see cref="FromModelGenerator"/>, and returns
    /// every source file the generator emitted (keyed by hint name).
    /// Throws if the post-generation compilation has errors.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> RunWithNullable(params string[] sources)
        => RunCore(NullableContextOptions.Enable, sources);

    /// <summary>
    /// Returns only the diagnostics reported by the generator itself (via ctx.ReportDiagnostic).
    /// Does not throw on compilation errors in the input, so it can be used to test GENxxx.
    /// </summary>
    internal static IReadOnlyList<Diagnostic> RunForDiagnostics(params string[] sources)
    {
        var compilation = CreateCompilation(sources, NullableContextOptions.Disable);
        var driver = CSharpGeneratorDriver
            .Create(new FromModelGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult().Diagnostics;
    }

    /// <summary>
    /// Use when testing generators that emit code referencing external SDK types (e.g. AWS, MongoDB).
    /// Skips the compilation-error check so tests can assert on generated text without providing stubs.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> RunUnchecked(params string[] sources)
    {
        var compilation = CreateCompilation(sources, NullableContextOptions.Disable);
        var driver = CSharpGeneratorDriver
            .Create(new FromModelGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver
            .GetRunResult()
            .Results
            .SelectMany(r => r.GeneratedSources)
            .ToDictionary(s => s.HintName, s => s.SourceText.ToString());
    }

    private static Dictionary<string, string> RunCore(NullableContextOptions nullable, string[] sources)
    {
        var compilation = CreateCompilation(sources, nullable);

        var generator = new FromModelGenerator();

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        var errors = updated.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Compilation has errors after generation:\n" + string.Join("\n", errors));

        return driver
            .GetRunResult()
            .Results
            .SelectMany(r => r.GeneratedSources)
            .ToDictionary(s => s.HintName, s => s.SourceText.ToString());
    }

    private static CSharpCompilation CreateCompilation(string[] sources, NullableContextOptions nullable)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        // Minimal reference set: mscorlib + System.Runtime (enough for most generator inputs).
        // Gener8.Abstractions is included so [FromModel], [TypeMapping], etc. are available
        // in test source code — they are no longer injected by the generator at runtime.
        // netstandard.dll is required because Abstractions.dll targets netstandard2.0; without it
        // Roslyn can't resolve System.Attribute's provenance and treats [FromModel] as an error type,
        // causing the generator to silently skip all annotated classes.
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FromModelAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: nullable));
    }
}
