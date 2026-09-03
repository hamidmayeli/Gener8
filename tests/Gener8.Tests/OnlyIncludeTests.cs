using Microsoft.CodeAnalysis;

namespace Gener8.Tests;

public class OnlyIncludeTests
{
    [Fact]
    public void OnlyInclude_SimpleNames_OnlyThosePropertiesGenerated()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } public string Sku { get; set; } = ""; }
            [FromModel(typeof(Product), OnlyInclude = [nameof(Product.Name), nameof(Product.Price)])]
            public partial class ProductDto { }
            """);

        var source = results["ProductDto.g.cs"];
        Assert.Contains("public string Name", source);
        Assert.Contains("public decimal Price", source);
        Assert.DoesNotContain("Sku", source);
    }

    [Fact]
    public void OnlyInclude_Extensions_OnlyMappedPropertiesAppear()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } public string Sku { get; set; } = ""; }
            [FromModel(typeof(Product), OnlyInclude = [nameof(Product.Name)])]
            public partial class ProductDto { }
            """);

        var ext = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("Name = dto.Name,", ext);
        Assert.Contains("Name = model.Name,", ext);
        Assert.DoesNotContain("Price", ext);
        Assert.DoesNotContain("Sku", ext);
    }

    [Fact]
    public void OnlyInclude_SingleProperty_Works()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Customer { public string Name { get; set; } = ""; public string Email { get; set; } = ""; }
            [FromModel(typeof(Customer), OnlyInclude = [nameof(Customer.Email)])]
            public partial class CustomerDto { }
            """);

        var source = results["CustomerDto.g.cs"];
        Assert.Contains("public string Email", source);
        Assert.DoesNotContain("Name", source);
    }

    [Fact]
    public void OnlyInclude_DottedPath_AutoDtoGetsOnlySubProperty()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string FullName { get; set; } = ""; public string Email { get; set; } = ""; }
                public class Order { public int Id { get; set; } public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order), OnlyInclude = ["Id", "Customer.FullName"])]
                public partial class OrderDto { }
            }
            """);

        // OrderDto should have Id and Customer
        var orderDto = results["MyApp.Dtos.OrderDto.g.cs"];
        Assert.Contains("public int Id", orderDto);
        Assert.Contains("CustomerDto", orderDto);

        // Auto-generated CustomerDto should only have FullName, not Email
        var customerDto = results.First(r => r.Key.EndsWith("CustomerDto.g.cs")).Value;
        Assert.Contains("public string FullName", customerDto);
        Assert.DoesNotContain("Email", customerDto);
    }

    [Fact]
    public void OnlyInclude_DottedPath_DeepNested_TwoLevels()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Address { public string Street { get; set; } = ""; public string PostCode { get; set; } = ""; }
                public class Customer { public string Name { get; set; } = ""; public Address Address { get; set; } = new(); }
                public class Order { public int Id { get; set; } public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order), OnlyInclude = ["Id", "Customer.Address.PostCode"])]
                public partial class OrderDto { }
            }
            """);

        // OrderDto has Id and Customer
        var orderDto = results["MyApp.Dtos.OrderDto.g.cs"];
        Assert.Contains("public int Id", orderDto);
        Assert.Contains("CustomerDto", orderDto);

        // CustomerDto should only have Address
        var customerDto = results.First(r => r.Key.EndsWith("CustomerDto.g.cs")).Value;
        Assert.Contains("AddressDto", customerDto);
        Assert.DoesNotContain("Name", customerDto);

        // AddressDto should only have PostCode
        var addressDto = results.First(r => r.Key.EndsWith("AddressDto.g.cs")).Value;
        Assert.Contains("public string PostCode", addressDto);
        Assert.DoesNotContain("Street", addressDto);
    }

    [Fact]
    public void OnlyInclude_PlainNameTakesPrecedenceOverDottedPath()
    {
        // "Customer" (plain) + "Customer.FullName" (dotted) → include all Customer properties
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string FullName { get; set; } = ""; public string Email { get; set; } = ""; }
                public class Order { public int Id { get; set; } public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order), OnlyInclude = ["Customer", "Customer.FullName"])]
                public partial class OrderDto { }
            }
            """);

        // CustomerDto should have BOTH FullName and Email because plain "Customer" takes precedence
        var customerDto = results.First(r => r.Key.EndsWith("CustomerDto.g.cs")).Value;
        Assert.Contains("public string FullName", customerDto);
        Assert.Contains("public string Email", customerDto);
    }

    [Fact]
    public void OnlyInclude_WithSuffixNaming_CorrectMethodName()
    {
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } }
            [FromModel(typeof(Product), OnlyInclude = [nameof(Product.Name)])]
            public partial class ProductView { }
            """);

        var ext = results["ProductViewExtensions.g.cs"];
        Assert.Contains("ToView(this global::Product? model)", ext);
        Assert.Contains("ToModel(this ProductView? dto)", ext);
    }

    [Fact]
    public void OnlyInclude_GEN004_WhenUsedWithIgnore()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } }
            [FromModel(typeof(Product), OnlyInclude = [nameof(Product.Name)], Ignore = [nameof(Product.Price)])]
            public partial class ProductDto { }
            """);

        var gen004 = Assert.Single(diagnostics, d => d.Id == "GEN004");
        Assert.Equal(DiagnosticSeverity.Error, gen004.Severity);
        Assert.Contains("ProductDto", gen004.GetMessage());
    }

    [Fact]
    public void OnlyInclude_GEN004_NoSourceEmitted()
    {
        var sources = GeneratorDriver.RunUnchecked("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } }
            [FromModel(typeof(Product), OnlyInclude = [nameof(Product.Name)], Ignore = [nameof(Product.Price)])]
            public partial class ProductDto { }
            """);

        Assert.DoesNotContain(sources.Keys, k => k.Contains("ProductDto"));
    }

    [Fact]
    public void OnlyInclude_GEN005_InvalidPropertyName()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), OnlyInclude = ["NonExistent"])]
            public partial class ProductDto { }
            """);

        var gen005 = Assert.Single(diagnostics, d => d.Id == "GEN005");
        Assert.Equal(DiagnosticSeverity.Error, gen005.Severity);
        Assert.Contains("NonExistent", gen005.GetMessage());
        Assert.Contains("Product", gen005.GetMessage());
    }

    [Fact]
    public void OnlyInclude_GEN005_ReportedForEachInvalidPath()
    {
        var diagnostics = GeneratorDriver.RunForDiagnostics("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), OnlyInclude = ["BadA", "BadB"])]
            public partial class ProductDto { }
            """);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "GEN005"));
    }

    [Fact]
    public void OnlyInclude_Empty_AllPropertiesIncluded()
    {
        // OnlyInclude = [] is equivalent to not setting OnlyInclude
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public decimal Price { get; set; } }
            [FromModel(typeof(Product), OnlyInclude = [])]
            public partial class ProductDto { }
            """);

        var source = results["ProductDto.g.cs"];
        Assert.Contains("public string Name", source);
        Assert.Contains("public decimal Price", source);
    }

    [Fact]
    public void OnlyInclude_MultipleSubPathsForSameNestedType()
    {
        // "Customer.FullName" and "Customer.Email" both constrain CustomerDto
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace MyApp.Models
            {
                public class Customer { public string FullName { get; set; } = ""; public string Email { get; set; } = ""; public string Phone { get; set; } = ""; }
                public class Order { public int Id { get; set; } public Customer Customer { get; set; } = new(); }
            }
            namespace MyApp.Dtos
            {
                [FromModel(typeof(MyApp.Models.Order), OnlyInclude = ["Customer.FullName", "Customer.Email"])]
                public partial class OrderDto { }
            }
            """);

        var customerDto = results.First(r => r.Key.EndsWith("CustomerDto.g.cs")).Value;
        Assert.Contains("public string FullName", customerDto);
        Assert.Contains("public string Email", customerDto);
        Assert.DoesNotContain("Phone", customerDto);
    }
}
