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
        Assert.Contains("public string Street", source);
        Assert.Contains("public string City", source);
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
        Assert.DoesNotContain("ShippingAddress", source);
        Assert.DoesNotContain("Address ", source);
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
        Assert.Contains("public TagDto Category", source);
        Assert.DoesNotContain("public Tag Category", source);
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
        Assert.Contains("public string Street", source);
        Assert.Contains("public string Phone", source);
        Assert.DoesNotContain("ShippingAddress", source);
        Assert.DoesNotContain("Recipient", source);
    }
}
