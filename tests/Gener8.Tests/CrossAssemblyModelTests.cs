using Microsoft.CodeAnalysis;

namespace Gener8.Tests;

public class CrossAssemblyModelTests
{
    [Fact]
    public void CrossAssembly_NonNullableInitProperty_EmitsRequired()
    {
        // ContractId has an initializer in the model source, but when the model is compiled into
        // a separate assembly the generator cannot read it — DeclaringSyntaxReferences is empty.
        // The generator should emit 'required' to prevent CS8618 in the consuming project.
        var results = GeneratorDriver.RunWithExternalModel(
            modelSource: """
                #nullable enable
                public sealed record Contract
                {
                    public string ContractId { get; init; } = System.Guid.NewGuid().ToString();
                    public required string TradingName { get; set; }
                    public string? Description { get; set; }
                }
                """,
            dtoSource: """
                using Gener8;
                [FromModel(typeof(Contract))]
                public partial class ContractDto { }
                """);

        var source = Assert.Single(results, r => r.Key == "ContractDto.g.cs").Value;
        // Non-nullable init property from a compiled assembly → required (cannot recover the initializer)
        Assert.Contains("public required string ContractId { get; init; }", source);
        // Already required on the model → stays required
        Assert.Contains("public required string TradingName { get; set; }", source);
        // Nullable → no required needed
        Assert.Contains("public string? Description { get; set; }", source);
    }

    [Fact]
    public void CrossAssembly_EmitsGEN007InfoDiagnostic()
    {
        var diagnostics = GeneratorDriver.RunWithExternalModelForDiagnostics(
            modelSource: """
                #nullable enable
                public class Order
                {
                    public string OrderId { get; set; } = "";
                    public string? Note { get; set; }
                }
                """,
            dtoSource: """
                using Gener8;
                [FromModel(typeof(Order))]
                public partial class OrderDto { }
                """);

        var gen007 = Assert.Single(diagnostics, d => d.Id == "GEN007");
        Assert.Equal(DiagnosticSeverity.Info, gen007.Severity);
        Assert.Contains("OrderId", gen007.GetMessage());
        Assert.Contains("Order", gen007.GetMessage());
        Assert.Contains("OrderDto", gen007.GetMessage());
    }

    [Fact]
    public void CrossAssembly_NonNullableSetProperty_EmitsRequired()
    {
        var results = GeneratorDriver.RunWithExternalModel(
            modelSource: """
                #nullable enable
                public class Order
                {
                    public string OrderId { get; set; } = "";
                    public int Quantity { get; set; }
                }
                """,
            dtoSource: """
                using Gener8;
                [FromModel(typeof(Order))]
                public partial class OrderDto { }
                """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        // Non-nullable reference type from compiled assembly → required
        Assert.Contains("public required string OrderId { get; set; }", source);
        // Value type → not affected by CS8618, no required needed
        Assert.DoesNotContain("required int", source);
        Assert.Contains("public int Quantity { get; set; }", source);
    }
}
