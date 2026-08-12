namespace FromModel.Tests;

public class FlattenTests
{
    [Fact]
    public void Flatten_ExpandsNestedPropertiesInline()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; public string City { get; set; } = ""; }
            public class Order { public string Reference { get; set; } = ""; public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public string Reference", source);
        Assert.Contains("public string ShippingAddressStreet", source);
        Assert.Contains("public string ShippingAddressCity", source);
    }

    [Fact]
    public void Flatten_OriginalPropertyNotEmitted()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.DoesNotContain("Address ShippingAddress", source);
        Assert.Contains("public string ShippingAddressStreet", source);
    }

    [Fact]
    public void Flatten_TypeMappingsApplyToFlattenedPropertyTypes()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Tag { public string Label { get; set; } = ""; }
            public partial class TagDto { }
            public class Address { public string Street { get; set; } = ""; public Tag Category { get; set; } = new(); }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
            [TypeMapping(typeof(Tag), typeof(TagDto))]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public TagDto ShippingAddressCategory", source);
        Assert.DoesNotContain("public Tag ShippingAddressCategory", source);
    }

    [Fact]
    public void Flatten_IgnoredPropertyIsDroppedNotFlattened()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Reference { get; set; } = ""; }
            [FromModel(typeof(Order), Ignore = [nameof(Order.ShippingAddress)], Flatten = [nameof(Order.ShippingAddress)])]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.DoesNotContain("ShippingAddress", source);
        Assert.DoesNotContain("Street", source);
        Assert.Contains("public string Reference", source);
    }

    [Fact]
    public void Flatten_MultipleEntries()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public class Contact { public string Phone { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); public Contact Recipient { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress), nameof(Order.Recipient)])]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public string ShippingAddressStreet", source);
        Assert.Contains("public string RecipientPhone", source);
        Assert.DoesNotContain("Address ShippingAddress", source);
        Assert.DoesNotContain("Contact Recipient", source);
    }

    [Fact]
    public void FlattenPrefix_ParentPrependsPropertyName()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; public string City { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)], FlattenPrefix = FlattenPrefix.Parent)]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public string ShippingAddressStreet", source);
        Assert.Contains("public string ShippingAddressCity", source);
        Assert.DoesNotContain("public string Street", source);
        Assert.DoesNotContain("public string City", source);
    }

    [Fact]
    public void FlattenPrefix_GapedPrependsPropertyNameWithUnderscore()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; public string City { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)], FlattenPrefix = FlattenPrefix.Gaped)]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public string ShippingAddress_Street", source);
        Assert.Contains("public string ShippingAddress_City", source);
        Assert.DoesNotContain("public string Street", source);
        Assert.DoesNotContain("public string City", source);
    }

    [Fact]
    public void FlattenPrefix_NoneUsesOriginalName()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string City { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)], FlattenPrefix = FlattenPrefix.None)]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public string City", source);
        Assert.DoesNotContain("ShippingAddress", source);
    }

    [Fact]
    public void EmitsFlattenPrefixEnumSourceFile()
    {
        var results = GeneratorDriver.Run("public class Empty { }");

        Assert.Contains("FlattenPrefix.g.cs", results.Keys);
    }
}
