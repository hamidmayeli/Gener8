namespace Gener8.Tests;

public class AutoDtoTests
{
    // -----------------------------------------------------------------------
    // Default behaviour: complex types in the same namespace get a DTO
    // -----------------------------------------------------------------------

    [Fact]
    public void GeneratesDtoForComplexPropertyTypeInSameNamespace()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        Assert.Contains(results.Keys, k => k.EndsWith("CustomerDto.g.cs"));
        Assert.Contains(results.Keys, k => k.EndsWith("CustomerDtoExtensions.g.cs"));
    }

    [Fact]
    public void GeneratedAutoDtoHasCorrectProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        var dtoSource = results.First(r => r.Key.EndsWith("CustomerDto.g.cs")).Value;
        Assert.Contains("public string Name", dtoSource);
    }

    [Fact]
    public void OrderDtoUsesCustomerDtoAsPropertyType()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        var orderDtoSource = results["MyApp.Dtos.OrderDto.g.cs"];
        Assert.Contains("CustomerDto", orderDtoSource);
        Assert.DoesNotContain("public MyApp.Models.Customer Customer", orderDtoSource);
    }

    [Fact]
    public void OrderDtoExtensionsCallsToDtoOnNestedProperty()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        var ext = results["MyApp.Dtos.OrderDtoExtensions.g.cs"];
        Assert.Contains(".ToDto()", ext);
        Assert.Contains(".ToModel()", ext);
    }

    [Theory]
    [InlineData("IList<Product>")]
    [InlineData("List<Product>")]
    [InlineData("Product[]")]
    public void GeneratesDtoForCollectionPropertiesWithComplexType(string type)
    {
        var results = GeneratorDriver.RunUnchecked($$"""
            using Gener8;
            using System.Collections.Generic;
            public class Product { public string Name { get; set; } }
            public class Order { public {{type}} Products { get; set; } }
            [FromModel(typeof(Order))]
            public partial class OrderDto { }
            """);
        var orderDtoSource = results.First(r => r.Key.EndsWith("OrderDto.g.cs")).Value;
        Assert.DoesNotContain($"public {type} Products", orderDtoSource);
        var productDto = results.FirstOrDefault(r => r.Key.EndsWith("ProductDto.g.cs"));
        Assert.NotEqual(default, productDto);
    }

    // -----------------------------------------------------------------------
    // Recursive auto-DTO: transitive complex types are also generated
    // -----------------------------------------------------------------------

    [Fact]
    public void GeneratesDtoForTransitiveComplexTypes()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Address { public string Street { get; set; } = ""; }
                public class Customer { public Address Address { get; set; } = new(); }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        Assert.Contains(results.Keys, k => k.EndsWith("CustomerDto.g.cs"));
        Assert.Contains(results.Keys, k => k.EndsWith("AddressDto.g.cs"));
    }

    // -----------------------------------------------------------------------
    // No auto-DTO for types in a different namespace
    // -----------------------------------------------------------------------

    [Fact]
    public void DoesNotGenerateDtoForTypeInDifferentNamespace()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace External { public class Tag { public string Label { get; set; } = ""; } }
            namespace MyApp.Models
            {
                public class Order { public External.Tag Tag { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        Assert.DoesNotContain("TagDto.g.cs", results.Keys);
    }

    [Fact]
    public void PreservesRawTypeForTypesInDifferentNamespace()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace External { public class Tag { public string Label { get; set; } = ""; } }
            namespace MyApp.Models
            {
                public class Order { public External.Tag Tag { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        var orderDtoSource = results["MyApp.Dtos.OrderDto.g.cs"];
        Assert.Contains("External.Tag", orderDtoSource);
    }

    // -----------------------------------------------------------------------
    // DtoNamespaces: opt-in for additional namespaces
    // -----------------------------------------------------------------------

    [Fact]
    public void GeneratesDtoForTypeInExplicitDtoNamespace()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace External { public class Tag { public string Label { get; set; } = ""; } }
            namespace MyApp.Models
            {
                public class Order { public External.Tag Tag { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order), DtoNamespaces = ["External"])]
                public partial class OrderDto { }
            }
            """);

        Assert.Contains(results.Keys, k => k.EndsWith("TagDto.g.cs"));
    }

    // -----------------------------------------------------------------------
    // No duplicate generation when user has manually decorated the nested DTO
    // -----------------------------------------------------------------------

    [Fact]
    public void SkipsAutoGenerationWhenUserHasDecoratedNestedDto()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Customer))]
                public partial class CustomerDto { }

                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        // CustomerDto.g.cs must appear exactly once (from the user's explicit decoration)
        var customerDtoFiles = results.Keys.Where(k => k.EndsWith("CustomerDto.g.cs")).ToList();
        Assert.Single(customerDtoFiles);
    }

    // -----------------------------------------------------------------------
    // Nullable complex property
    // -----------------------------------------------------------------------

    [Fact]
    public void HandlesNullableComplexProperty()
    {
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string Name { get; set; } = ""; }
                public class Order { public Customer? Customer { get; set; } }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        var orderDtoSource = results["MyApp.Dtos.OrderDto.g.cs"];
        Assert.Contains("CustomerDto?", orderDtoSource);
    }

    // -----------------------------------------------------------------------
    // Collection of complex type
    // -----------------------------------------------------------------------

    [Fact]
    public void GeneratesDtoForCollectionElementType()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            namespace MyApp.Models
            {
                public class Item { public int Price { get; set; } }
                public class Order { public List<Item> Items { get; set; } = []; }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order))]
                public partial class OrderDto { }
            }
            """);

        Assert.Contains(results.Keys, k => k.EndsWith("ItemDto.g.cs"));

        var orderDtoSource = results["MyApp.Dtos.OrderDto.g.cs"];
        Assert.Contains("ItemDto", orderDtoSource);
    }

    // -----------------------------------------------------------------------
    // Global namespace: types without a namespace still qualify
    // -----------------------------------------------------------------------

    [Fact]
    public void GlobalNamespaceTypesQualify()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Customer { public string Name { get; set; } = ""; }
            public class Order { public Customer Customer { get; set; } = new(); }
            [FromModel(typeof(Order))]
            public partial class OrderDto { }
            """);

        Assert.Contains("CustomerDto.g.cs", results.Keys);
    }

    // -----------------------------------------------------------------------
    // Explicit TypeMapping overrides auto-DTO for that type
    // -----------------------------------------------------------------------

    [Fact]
    public void ExplicitTypeMappingPreventsAutoDtoGeneration()
    {
        // RunUnchecked: explicit [TypeMapping] references types without generated extension methods,
        // so the resulting compilation has unresolved method calls that we don't need to satisfy here.
        var results = GeneratorDriver.RunUnchecked("""
            using Gener8;
            public class Customer { public string Name { get; set; } = ""; }
            public class MyCustomerDto { public string Name { get; set; } = ""; }
            public class Order { public Customer Customer { get; set; } = new(); }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Customer), typeof(MyCustomerDto))]
            public partial class OrderDto { }
            """);

        Assert.DoesNotContain("CustomerDto.g.cs", results.Keys);

        var orderDtoSource = results["OrderDto.g.cs"];
        Assert.Contains("MyCustomerDto", orderDtoSource);
    }
}
