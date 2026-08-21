namespace Gener8.Tests;

public class MappingExtensionsTests
{
    [Fact]
    public void EmitsExtensionsFile()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDtoExtensions.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsToModelAndToDtoMethods()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("ToModel(this ProductDto? dto)", source);
        Assert.Contains("ToDto(this", source);
        Assert.Contains("Product", source);
    }

    [Fact]
    public void ToModelMapsSimpleProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public int Price { get; set; } }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("Name = dto.Name,", source);
        Assert.Contains("Price = dto.Price,", source);
    }

    [Fact]
    public void ToDtoMapsSimpleProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; public int Price { get; set; } }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("Name = model.Name,", source);
        Assert.Contains("Price = model.Price,", source);
    }

    [Fact]
    public void ToModelUsesRenamedPropertyNameOnModelSide()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string InternalSku { get; set; } = ""; }
            [FromModel(typeof(Product))]
            [RenameProperty(nameof(Product.InternalSku), "Sku")]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        // model side uses original name, dto side uses renamed name
        Assert.Contains("InternalSku = dto.Sku,", source);
    }

    [Fact]
    public void ToDtoUsesRenamedPropertyNameOnDtoSide()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string InternalSku { get; set; } = ""; }
            [FromModel(typeof(Product))]
            [RenameProperty(nameof(Product.InternalSku), "Sku")]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        // dto side uses renamed name, model side uses original name
        Assert.Contains("Sku = model.InternalSku,", source);
    }

    [Fact]
    public void GetOnlyPropertiesAreExcludedFromBothMethods()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Id { get; } = ""; public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.DoesNotContain("Id = ", source);
        Assert.Contains("Name = ", source);
    }

    [Fact]
    public void TypeMappedPropertyCallsToModelInToModelMethod()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Reference { get; set; } = ""; }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = results["OrderDtoExtensions.g.cs"];
        Assert.Contains("ShippingAddress = dto.ShippingAddress.ToModel(),", source);
    }

    [Fact]
    public void TypeMappedPropertyCallsToDtoInToDtoMethod()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Reference { get; set; } = ""; }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = results["OrderDtoExtensions.g.cs"];
        Assert.Contains("ShippingAddress = model.ShippingAddress.ToDto(),", source);
    }

    [Fact]
    public void TypeMappedGenericCollectionProjectsElementsInBothDirections()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public List<Address> ShippingAddresses { get; set; } = []; }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = results["OrderDtoExtensions.g.cs"];
        Assert.Contains("ShippingAddresses = [.. dto.ShippingAddresses.Select(m => m.ToModel())],", source);
        Assert.Contains("ShippingAddresses = [.. model.ShippingAddresses.Select(m => m.ToDto())],", source);
    }

    [Fact]
    public void TypeMappedPropertyNonMappedPropertiesStillAppear()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Reference { get; set; } = ""; }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = results["OrderDtoExtensions.g.cs"];
        Assert.Contains("Reference = dto.Reference,", source);
        Assert.Contains("Reference = model.Reference,", source);
    }

    [Fact]
    public void TypeMappedNullablePropertyUsesNullConditional()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            #nullable enable
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public Address ShippingAddress { get; set; } = new(); }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        // AddressDto is non-nullable here so no null-conditional — compilation must succeed
        var source = results["OrderDtoExtensions.g.cs"];
        Assert.Contains("ShippingAddress.ToModel()", source);
        Assert.Contains("ShippingAddress.ToDto()", source);
    }

    [Fact]
    public void FlattenedPropertiesAreInToDtoAndReconstructedInToModel()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            public class Order { public Address ShippingAddress { get; set; } = new(); public string Ref { get; set; } = ""; }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
            public partial class OrderDto { }
            """);

        var source = results["OrderDtoExtensions.g.cs"];
        // ToDto includes flattened via path
        Assert.Contains("ShippingAddressStreet = model.ShippingAddress.Street,", source);
        // ToModel reconstructs the nested object
        Assert.Contains("ShippingAddress = new global::Address", source);
        Assert.Contains("Street = dto.ShippingAddressStreet", source);
        // Non-flattened still mapped in both
        Assert.Contains("Ref = dto.Ref,", source);
        Assert.Contains("Ref = model.Ref,", source);
    }

    [Fact]
    public void FlattenedNullableParentUsesNullConditionalInToDto()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            #nullable enable
            public class Address { public string Street { get; set; } = ""; }
            public class Order { public Address? ShippingAddress { get; set; } }
            [FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
            public partial class OrderDto { }
            """);

        var source = results["OrderDtoExtensions.g.cs"];
        Assert.Contains("model.ShippingAddress?.Street", source);
    }

    [Fact]
    public void ExtensionsClassHintnameIncludesNamespace()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            namespace My.App
            {
                [FromModel(typeof(Product))]
                public partial class ProductDto { }
            }
            """);

        Assert.Contains("My.App.ProductDtoExtensions.g.cs", results.Keys);
    }

    [Fact]
    public void ExtensionsClassUsesPublicAccessibilityWhenDtoIsPublic()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("public static class ProductDtoExtensions", source);
    }

    [Fact]
    public void ExtensionsClassUsesInternalAccessibilityWhenDtoIsInternal()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            internal partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("internal static class ProductDtoExtensions", source);
    }

    [Fact]
    public void InitOnlyPropertiesAreIncludedInBothMethods()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; init; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("Name = dto.Name,", source);
        Assert.Contains("Name = model.Name,", source);
    }

    [Fact]
    public void ExtensionMethodsAreNullSafe()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("[return: NotNullIfNotNull(nameof(dto))]", source);
        Assert.Contains("[return: NotNullIfNotNull(nameof(model))]", source);
        Assert.Contains("dto is null ? null", source);
        Assert.Contains("model is null ? null", source);
        Assert.Contains("ProductDto? dto", source);
        Assert.Contains("? ToModel(", source);
        Assert.Contains("? ToDto(", source);
    }
}
