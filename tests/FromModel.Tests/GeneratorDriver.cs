using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace FromModel.Tests;

internal static class GeneratorDriver
{
    /// <summary>
    /// Compiles <paramref name="sources"/>, runs <see cref="FromModelGenerator"/>, and returns
    /// every source file the generator emitted (keyed by hint name).
    /// Throws if the input compilation itself has errors (generator bugs would be masked otherwise).
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Run(params string[] sources)
    {
        var compilation = CreateCompilation(sources);

        var generator = new FromModelGenerator();

        // RunGeneratorsAndUpdateCompilation returns a compilation that includes the
        // generator-injected sources (e.g. FromModelAttribute from RegisterPostInitializationOutput).
        // Check errors on that, not on the pre-generation compilation.
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

    private static CSharpCompilation CreateCompilation(string[] sources)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        // Minimal reference set: mscorlib + System.Runtime (enough for most generator inputs).
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
