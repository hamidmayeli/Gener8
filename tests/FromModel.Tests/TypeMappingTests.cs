namespace FromModel.Tests;

public class TypeMappingTests
{
    [Fact]
    public void MapsPropertyTypeToDto()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public partial class AddressDto { }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Reference { get; set; } = ""; }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public AddressDto ShippingAddress", source);
        Assert.Contains("public string Reference", source);
        Assert.DoesNotContain("public Address ShippingAddress", source);
    }

    [Fact]
    public void MultipleMappingsApplied()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public partial class AddressDto { }
            public class Customer { public string Name { get; set; } = ""; }
            public partial class CustomerDto { }
            public class Order
            {
                public Address ShippingAddress { get; set; } = new();
                public Customer Buyer { get; set; } = new();
                public string Reference { get; set; } = "";
            }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            [TypeMapping(typeof(Customer), typeof(CustomerDto))]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public AddressDto ShippingAddress", source);
        Assert.Contains("public CustomerDto Buyer", source);
        Assert.Contains("public string Reference", source);
    }

    [Fact]
    public void UnmappedTypesArePreserved()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class Address { public string Street { get; set; } = ""; }
            public partial class AddressDto { }
            public class Tag { public string Label { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); public Tag Category { get; set; } = new(); }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public AddressDto ShippingAddress", source);
        Assert.Contains("public Tag Category", source);
    }

    [Fact]
    public void EmitsTypeMappingAttributeSourceFile()
    {
        var results = GeneratorDriver.Run("public class Empty { }");

        Assert.Contains("TypeMappingAttribute.g.cs", results.Keys);
    }
}
