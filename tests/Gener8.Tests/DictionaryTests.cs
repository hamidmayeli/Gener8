using Microsoft.CodeAnalysis;

namespace Gener8.Tests;

public class DictionaryTests
{
    [Theory]
    [InlineData("System.Collections.Generic.Dictionary<string, int>")]
    [InlineData("System.Collections.Generic.IDictionary<string, int>")]
    [InlineData("System.Collections.Generic.IDictionary<int, int>")]
    [InlineData("System.Collections.Generic.IReadOnlyDictionary<int, int>")]
    [InlineData("System.Collections.Generic.SortedList<int, int>")]
    public void EmitsDtoClassForDictionary(string typeName)
    {
        var sources = GeneratorDriver.Run($$"""
            using Gener8;
            using System.Collections.Generic;

            namespace Domain
            {
                public class AttributeCollection
                {
                    public {{typeName}} KeyValuePairs { get; set; }
                }
            }

            namespace Api
            {
                [FromModel(typeof(Domain.AttributeCollection))]
                public partial class AttributeCollectionViewModel { }
            }
            """);

        Assert.Contains(sources.Keys, k => k.EndsWith("AttributeCollectionViewModel.g.cs"));
        Assert.Contains(sources.Values, v => v.Contains("public partial class AttributeCollectionViewModel"));
        Assert.Contains(sources.Values, v => v.Contains($"public {typeName} KeyValuePairs {{ get; set; }}"));
    }

    [Fact]
    public void EmitsDtoClassForComplexKeyOfDictionary()
    {
        var sources = GeneratorDriver.Run($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public record ComplexKey
                {
                    public int Id { get; set; }
                    public string Name { get; set; }
                }
                public class AttributeCollection
                {
                    public Dictionary<ComplexKey, int> KeyValuePairs { get; set; }
                }
            }
            namespace Api
            {
                [FromModel(typeof(Domain.AttributeCollection), DtoNamespaces = ["Domain"])]
                public partial class AttributeCollectionViewModel { }
            }
            """);
        Assert.Contains(sources.Keys, k => k.EndsWith("AttributeCollectionViewModel.g.cs"));
        Assert.Contains(sources.Values, v => v.Contains("public partial class AttributeCollectionViewModel"));
        Assert.Contains(sources.Values, v => v.Contains("public System.Collections.Generic.Dictionary<ComplexKeyViewModel, int> KeyValuePairs { get; set; }"));
    }

    [Fact]
    public void EmitsDtoClassForComplexValueOfDictionary()
    {
        var sources = GeneratorDriver.Run($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public record ComplexValue
                {
                    public int Id { get; set; }
                    public string Name { get; set; }
                }
                public class AttributeCollection
                {
                    public Dictionary<int, ComplexValue> KeyValuePairs { get; set; }
                }
            }
            namespace Api
            {
                [FromModel(typeof(Domain.AttributeCollection), DtoNamespaces = ["Domain"])]
                public partial class AttributeCollectionViewModel { }
            }
            """);

        Assert.Contains(sources.Keys, k => k.EndsWith("AttributeCollectionViewModel.g.cs"));
        Assert.Contains(sources.Values, v => v.Contains("public partial class AttributeCollectionViewModel"));
        Assert.Contains(sources.Values, v => v.Contains("public System.Collections.Generic.Dictionary<int, ComplexValueViewModel> KeyValuePairs { get; set; }"));
    }

    [Fact]
    public void EmitsExtensionMethodForDictionary()
    {
        var sources = GeneratorDriver.Run($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public class AttributeCollection
                {
                    public Dictionary<string, int> KeyValuePairs { get; set; }
                }
            }
            namespace Data
            {
                [FromModel(typeof(Domain.AttributeCollection))]
                public partial class AttributeCollectionDto { }
            }
            """);

        Assert.Contains(sources.Keys, k => k.EndsWith("AttributeCollectionDtoExtensions.g.cs"));
        Assert.Contains(sources.Values, v => v.Contains("public static partial class AttributeCollectionDtoExtensions"));
        Assert.Contains(sources.Values, v => v.Contains("ToModel(this AttributeCollectionDto dto)"));
        Assert.Contains(sources.Values, v => v.Contains("ToDto(this") && v.Contains("AttributeCollection model)"));
    }

    [Fact]
    public void EmitsExtensionMethodBodyForComplexValueDictionary()
    {
        var sources = GeneratorDriver.Run($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public record ComplexValue { public int Id { get; set; } }
                public class AttributeCollection
                {
                    public Dictionary<int, ComplexValue> KeyValuePairs { get; set; }
                }
            }
            namespace Data
            {
                [FromModel(typeof(Domain.AttributeCollection), DtoNamespaces = ["Domain"])]
                public partial class AttributeCollectionDto { }
            }
            """);

        var ext = Assert.Single(sources, r => r.Key.EndsWith("AttributeCollectionDtoExtensions.g.cs")).Value;
        // ToModel: DTO Dictionary<int,ComplexValueDto> -> model Dictionary<int,ComplexValue>
        Assert.Contains("ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToModel())", ext);
        // ToDto: model Dictionary<int,ComplexValue> -> DTO Dictionary<int,ComplexValueDto>
        Assert.Contains("ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToDto())", ext);
    }

    [Fact]
    public void EmitsConcreteTypeForIDictionaryWithDynamoDBRepository()
    {
        var sources = GeneratorDriver.RunUnchecked($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public class AttributeCollection
                {
                    public IDictionary<string, int> KeyValuePairs { get; set; }
                }
            }
            namespace Data
            {
                [FromModel(typeof(Domain.AttributeCollection), Repository = RepositoryType.DynamoDb)]
                public partial class AttributeCollectionDto { }
            }
            """);

        Assert.Contains(sources.Keys, k => k.EndsWith("AttributeCollectionDto.g.cs"));
        Assert.Contains(sources.Values, v => v.Contains("public System.Collections.Generic.Dictionary<string, int> KeyValuePairs { get; set; }"));
    }

    [Fact]
    public void EmitsExtensionMethodBodyForDynamoDbDictionaryRemap()
    {
        var sources = GeneratorDriver.RunUnchecked($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public class AttributeCollection
                {
                    public IDictionary<string, int> KeyValuePairs { get; set; }
                }
            }
            namespace Data
            {
                [FromModel(typeof(Domain.AttributeCollection), Repository = RepositoryType.DynamoDb)]
                public partial class AttributeCollectionDto { }
            }
            """);

        var ext = Assert.Single(sources, r => r.Key.EndsWith("AttributeCollectionDtoExtensions.g.cs")).Value;
        // ToDto: model IDictionary<string,int> -> DTO Dictionary<string,int> via ToDictionary
        Assert.Contains("ToDictionary(kvp => kvp.Key, kvp => kvp.Value)", ext);
    }

    [Fact]
    public void ReportGEN006WhenDictionaryKeyIsNotStringWithDynamoDBRepository()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics($$"""
            using Gener8;
            using System.Collections.Generic;
            namespace Domain
            {
                public class AttributeCollection
                {
                    public IDictionary<int, int> KeyValuePairs { get; set; }
                }
            }
            namespace Data
            {
                [FromModel(typeof(Domain.AttributeCollection), Repository = RepositoryType.DynamoDb)]
                public partial class AttributeCollectionDto { }
            }
            """);

        var gen006 = Assert.Single(diagnostics, d => d.Id == "GEN006");
        Assert.Equal(DiagnosticSeverity.Error, gen006.Severity);
        Assert.Contains("non-string key", gen006.GetMessage());
    }
}
