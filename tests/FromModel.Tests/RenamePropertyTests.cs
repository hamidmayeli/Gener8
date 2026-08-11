namespace FromModel.Tests;

public class RenamePropertyTests
{
    [Fact]
    public void RenameProperty_UsesTargetNameInOutput()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Product { public string InternalSku { get; set; } = ""; }
            [FromModel(typeof(Product))]
            [RenameProperty(nameof(Product.InternalSku), "Sku")]
            public partial class ProductDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "ProductDto.g.cs").Value;
        Assert.Contains("public string Sku", source);
        Assert.DoesNotContain("InternalSku", source);
    }

    [Fact]
    public void RenameProperty_MultipleRenamesApplied()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Product { public string InternalSku { get; set; } = ""; public string DisplayName { get; set; } = ""; }
            [FromModel(typeof(Product))]
            [RenameProperty(nameof(Product.InternalSku), "Sku")]
            [RenameProperty(nameof(Product.DisplayName), "Name")]
            public partial class ProductDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "ProductDto.g.cs").Value;
        Assert.Contains("public string Sku", source);
        Assert.Contains("public string Name", source);
        Assert.DoesNotContain("InternalSku", source);
        Assert.DoesNotContain("DisplayName", source);
    }

    [Fact]
    public void RenameProperty_DoesNotApplyToFlattenedProperties()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Reference { get; set; } = ""; }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
            [RenameProperty(nameof(Order.Reference), "Ref")]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public string ShippingAddressStreet", source);
        Assert.Contains("public string Ref", source);
        Assert.DoesNotContain("Reference", source);
    }

    [Fact]
    public void RenameProperty_UnknownSourceNameIsIgnored()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            [RenameProperty("NonExistent", "Whatever")]
            public partial class ProductDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "ProductDto.g.cs").Value;
        Assert.Contains("public string Name", source);
    }

    [Fact]
    public void EmitsRenamePropertyAttributeSourceFile()
    {
        var results = GeneratorDriver.Run("public class Empty { }");

        Assert.Contains("RenamePropertyAttribute.g.cs", results.Keys);
    }
}
