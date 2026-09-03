namespace Gener8.Tests;

public class SuffixedDtoNameTests
{
    [Fact]
    public void PrefixMatchUsesModelNameSuffix()
    {
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductView { }
            """);

        var source = results["ProductViewExtensions.g.cs"];
        Assert.Contains("ToView(this global::Product? model)", source);
    }

    [Fact]
    public void PrefixMatchToModelMethodRemainsToModel()
    {
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductView { }
            """);

        var source = results["ProductViewExtensions.g.cs"];
        Assert.Contains("ToModel(this ProductView? dto)", source);
    }

    [Fact]
    public void StandardDtoSuffixStillGeneratesToDto()
    {
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("ToDto(this global::Product? model)", source);
    }

    [Fact]
    public void NoPrefixMatchFallsBackToToDto()
    {
        // DTO name does not start with model name → falls back to "ToDto"
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class CatalogItem { }
            """);

        var source = results["CatalogItemExtensions.g.cs"];
        Assert.Contains("ToDto(this global::Product? model)", source);
    }

    [Fact]
    public void ExactSameNameFallsBackToToDto()
    {
        // DTO name equals model name (zero-length suffix) → falls back to "ToDto"
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            namespace Models { public class Product { public string Name { get; set; } = ""; } }
            namespace Dtos
            {
                using Gener8;
                [FromModel(typeof(Models.Product))]
                public partial class Product { }
            }
            """);

        var source = results["Dtos.ProductExtensions.g.cs"];
        Assert.Contains("ToDto(this", source);
    }

    [Fact]
    public void SuffixWorksWithArbitraryName()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Order { public int Id { get; set; } }
            [FromModel(typeof(Order))]
            public partial class OrderSummary { }
            """);

        var source = results["OrderSummaryExtensions.g.cs"];
        Assert.Contains("ToSummary(this global::Order", source);
        Assert.Contains("ToModel(this OrderSummary", source);
    }

    [Fact]
    public void AutoDtoPropertyFollowTheSameSuffix()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Views
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderView { }
            }
            """);

        Assert.Contains(results.Keys, k => k.EndsWith("CustomerView.g.cs"));
        Assert.Contains(results.Keys, k => k.EndsWith("CustomerViewExtensions.g.cs"));

        var source = results.First(r => r.Key.EndsWith("CustomerViewExtensions.g.cs")).Value;
        Assert.Contains("ToView(this global::MyApp.Models.Customer", source);
        Assert.Contains("ToModel(this CustomerView", source);
    }

    [Fact]
    public void AutoDtoPropertyCallsCorrectSuffixedMethod()
    {
        // When the nested mapped DTO also follows the suffix convention, the chained call
        // inside ToView() should call .ToView() not .ToDto().
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            public partial class AddressView { }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order))]
            public partial class OrderView { }
            """);

        var source = results["OrderViewExtensions.g.cs"];
        Assert.Contains("ShippingAddress = model.ShippingAddress.ToView(),", source);
    }

    [Fact]
    public void TypeMappedNestedPropertyCallsCorrectSuffixedMethod()
    {
        // When the nested mapped DTO also follows the suffix convention, the chained call
        // inside ToView() should call .ToView() not .ToDto().
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressView { }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressView))]
            public partial class OrderView { }
            """);

        var source = results["OrderViewExtensions.g.cs"];
        Assert.Contains("ShippingAddress = model.ShippingAddress.ToView(),", source);
    }

    [Fact]
    public void TypeMappedGenericCollectionCallsCorrectSuffixedMethod()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            public class Tag { public string Name { get; set; } = ""; }
            [FromModel(typeof(Tag))]
            public partial class TagView { }
            public class Post { public List<Tag> Tags { get; set; } = []; }
            [FromModel(typeof(Post))]
            [TypeMapping(typeof(Tag), typeof(TagView))]
            public partial class PostView { }
            """);

        var source = results["PostViewExtensions.g.cs"];
        Assert.Contains("Tags = [.. model.Tags.Select(m => m.ToView())],", source);
        Assert.Contains("Tags = [.. dto.Tags.Select(m => m.ToModel())],", source);
    }

    [Fact]
    public void RepositoryOverrideBodyCallsCorrectSuffixedMethod()
    {
        var results = GeneratorDriver.RunUnchecked("""
            using Gener8;
            public class Product { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.Custom)]
            public partial class ProductView { }
            """);

        var source = results["ProductRepository.g.cs"];
        // Override signature stays as "ToDto" (matches the abstract base); body delegates to .ToView()
        Assert.Contains("model.ToView()", source);
    }

    [Fact]
    public void SuffixMappingMapsSimplePropertiesCorrectly()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public int Price { get; set; } }
            [FromModel(typeof(Product))]
            public partial class ProductView { }
            """);

        var source = results["ProductViewExtensions.g.cs"];
        Assert.Contains("Name = dto.Name,", source);
        Assert.Contains("Price = dto.Price,", source);
        Assert.Contains("Name = model.Name,", source);
        Assert.Contains("Price = model.Price,", source);
    }
}
