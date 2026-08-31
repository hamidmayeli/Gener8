using Microsoft.CodeAnalysis;

namespace Gener8.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void ReportsGEN001WhenModelTypeIsUnresolvable()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            [FromModel(typeof(NonExistentModel))]
            public partial class MyDto { }
            """);

        var gen001 = Assert.Single(diagnostics, d => d.Id == "GEN001");
        Assert.Equal(DiagnosticSeverity.Error, gen001.Severity);
        Assert.Contains("MyDto", gen001.GetMessage());
    }

    [Fact]
    public void NoSourceEmittedForDtoWhenModelTypeIsUnresolvable()
    {
        var sources = GeneratorDriver.RunUnchecked("""
            using Gener8;
            [FromModel(typeof(NonExistentModel))]
            public partial class MyDto { }
            """);

        Assert.DoesNotContain(sources.Keys, k => k.Contains("MyDto"));
    }

    [Fact]
    public void NoGeneratorDiagnosticsForValidModel()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            public class TheModel { public string Name { get; set; } = ""; }
            [FromModel(typeof(TheModel))]
            public partial class TheDto { }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GEN001IncludesTheDtoClassName()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            [FromModel(typeof(DoesNotExist))]
            public partial class CustomerDto { }
            """);

        var gen001 = Assert.Single(diagnostics, d => d.Id == "GEN001");
        Assert.Contains("CustomerDto", gen001.GetMessage());
    }

    [Fact]
    public void ReportsGEN002WhenForceNullablePropertyIsAlreadyNullable()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            #nullable enable
            public class TheModel { public string? Name { get; set; } }
            [FromModel(typeof(TheModel), ForceNullable = [nameof(TheModel.Name)])]
            public partial class TheDto { }
            """);

        var gen002 = Assert.Single(diagnostics, d => d.Id == "GEN002");
        Assert.Equal(DiagnosticSeverity.Error, gen002.Severity);
        Assert.Contains("Name", gen002.GetMessage());
        Assert.Contains("TheModel", gen002.GetMessage());
    }

    [Fact]
    public void GEN002ReportedForEachAlreadyNullableProperty()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            #nullable enable
            public class TheModel { public string? A { get; set; } public string? B { get; set; } }
            [FromModel(typeof(TheModel), ForceNullable = [nameof(TheModel.A), nameof(TheModel.B)])]
            public partial class TheDto { }
            """);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "GEN002"));
    }

    [Fact]
    public void NoSourceEmittedWhenGEN002IsReported()
    {
        var sources = GeneratorDriver.RunUnchecked("""
            using Gener8;
            #nullable enable
            public class TheModel { public string? Name { get; set; } }
            [FromModel(typeof(TheModel), ForceNullable = [nameof(TheModel.Name)])]
            public partial class TheDto { }
            """);

        Assert.DoesNotContain(sources.Keys, k => k.Contains("TheDto"));
    }

    [Fact]
    public void GEN001ReportedOncePerUnresolvableDto()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            [FromModel(typeof(MissingA))]
            public partial class DtoA { }
            [FromModel(typeof(MissingB))]
            public partial class DtoB { }
            """);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "GEN001"));
    }
}
