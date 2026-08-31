namespace Gener8.Tests;

public class ForceNullableTests
{
    [Fact]
    public void MakesStringPropertyNullableInDto()
    {
        // RunUnchecked: partial method stubs require a consumer implementation; compilation would fail otherwise.
        var generated = GeneratorDriver.RunUnchecked("""
            using Gener8;
            public class Item { public string Name { get; set; } = ""; }
            [FromModel(typeof(Item), ForceNullable = [nameof(Item.Name)])]
            public partial class ItemDto { }
            """);

        Assert.Contains("string? Name", generated["ItemDto.g.cs"]);
    }

    [Fact]
    public void MakesValueTypePropertyNullableInDto()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Order { public int Count { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(Order), ForceNullable = [nameof(Order.Count)])]
            public partial class OrderDto { }
            """);

        Assert.Contains("int? Count", generated["OrderDto.g.cs"]);
    }

    [Fact]
    public void SuppressesRequiredModifier()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Item { public required string Name { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(Item), ForceNullable = [nameof(Item.Name)])]
            public partial class ItemDto { }
            """);

        var dtoSource = generated["ItemDto.g.cs"];
        Assert.DoesNotContain("required string? Name", dtoSource);
        Assert.Contains("string? Name", dtoSource);
    }

    [Fact]
    public void EmitsPartialMethodStubForForceNullableProperty()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Order { public int Count { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(Order), ForceNullable = [nameof(Order.Count)])]
            public partial class OrderDto { }
            """);

        Assert.Contains("private static partial int GetDefaultCount(OrderDto dto);", generated["OrderDtoExtensions.g.cs"]);
    }

    [Fact]
    public void ToModelUsesNullCheckForValueTypeForceNullable()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Order { public int Count { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(Order), ForceNullable = [nameof(Order.Count)])]
            public partial class OrderDto { }
            """);

        Assert.Contains("dto.Count is null ? GetDefaultCount(dto) : dto.Count.Value", generated["OrderDtoExtensions.g.cs"]);
    }

    [Fact]
    public void ToModelUsesNullCheckForStringForceNullable()
    {
        var generated = GeneratorDriver.RunUnchecked("""
            using Gener8;
            public class Item { public string Name { get; set; } = ""; }
            [FromModel(typeof(Item), ForceNullable = [nameof(Item.Name)])]
            public partial class ItemDto { }
            """);

        Assert.Contains("dto.Name is null ? GetDefaultName(dto) : dto.Name", generated["ItemDtoExtensions.g.cs"]);
    }

    [Fact]
    public void ToModelUsesNullCheckWithToModelCallWhenTypeMapped()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Catalog { public CatalogVersion Version { get; set; } = new(); } public class CatalogVersion { public int Major { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(CatalogVersion))]
            public partial class CatalogVersionDto { }
            """,
            """
            using Gener8;
            [FromModel(typeof(Catalog), ForceNullable = [nameof(Catalog.Version)])]
            [TypeMapping(typeof(CatalogVersion), typeof(CatalogVersionDto))]
            public partial class CatalogDto { }
            """);

        Assert.Contains("dto.Version is null ? GetDefaultVersion(dto) : dto.Version.ToModel()", generated["CatalogDtoExtensions.g.cs"]);
    }

    [Fact]
    public void ToDtoUsesDirectAssignmentWithoutNullConditional()
    {
        // Use a value type to avoid inferred TypeMapping and verify no null-conditional in ToDto.
        var generated = GeneratorDriver.RunUnchecked(
            "public class Order { public int Count { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(Order), ForceNullable = [nameof(Order.Count)])]
            public partial class OrderDto { }
            """);

        var extSource = generated["OrderDtoExtensions.g.cs"];
        // The model's Count is non-nullable; ToDto must not emit "model.Count?" or similar.
        Assert.Contains("Count = model.Count,", extSource);
        Assert.DoesNotContain("model.Count?", extSource);
    }

    [Fact]
    public void ToDtoUsesDirectToDtoCallWhenTypeMapped()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Catalog { public CatalogVersion Version { get; set; } = new(); } public class CatalogVersion { public int Major { get; set; } }",
            """
            using Gener8;
            [FromModel(typeof(CatalogVersion))]
            public partial class CatalogVersionDto { }
            """,
            """
            using Gener8;
            [FromModel(typeof(Catalog), ForceNullable = [nameof(Catalog.Version)])]
            [TypeMapping(typeof(CatalogVersion), typeof(CatalogVersionDto))]
            public partial class CatalogDto { }
            """);

        Assert.Contains("Version = model.Version.ToDto(),", generated["CatalogDtoExtensions.g.cs"]);
    }

    [Fact]
    public void MultipleForceNullablePropertiesEmitMultipleStubs()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Order { public int Count { get; set; } public string Note { get; set; } = \"\"; }",
            """
            using Gener8;
            [FromModel(typeof(Order), ForceNullable = [nameof(Order.Count), nameof(Order.Note)])]
            public partial class OrderDto { }
            """);

        var extSource = generated["OrderDtoExtensions.g.cs"];
        Assert.Contains("private static partial int GetDefaultCount(OrderDto dto);", extSource);
        Assert.Contains("private static partial string GetDefaultNote(OrderDto dto);", extSource);
    }

    [Fact]
    public void NonForceNullablePropertiesAreUnaffected()
    {
        var generated = GeneratorDriver.RunUnchecked(
            "public class Order { public int Count { get; set; } public string Name { get; set; } = \"\"; }",
            """
            using Gener8;
            [FromModel(typeof(Order), ForceNullable = [nameof(Order.Count)])]
            public partial class OrderDto { }
            """);

        var dtoSource = generated["OrderDto.g.cs"];
        var extSource = generated["OrderDtoExtensions.g.cs"];

        Assert.Contains("int? Count", dtoSource);
        Assert.Contains("string Name", dtoSource);
        Assert.DoesNotContain("string? Name", dtoSource);
        Assert.Contains("Name = dto.Name,", extSource);
        Assert.DoesNotContain("GetName(dto)", extSource);
    }
}
