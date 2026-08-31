namespace Gener8.Tests;

public class TypeMappingTests
{
    [Fact]
    public void MapsPropertyTypeToDto()
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

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public AddressDto ShippingAddress", source);
        Assert.Contains("public string Reference", source);
        Assert.DoesNotContain("public Address ShippingAddress", source);
    }

    [Fact]
    public void MultipleMappingsApplied()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Customer { public string Name { get; set; } = ""; }
            [FromModel(typeof(Customer))]
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
        // Tag lives in a different namespace so it is NOT in the qualifying namespace and
        // must be preserved as the raw type (no auto-mapping). Address is in global namespace
        // (same as Order) but is covered by an explicit [TypeMapping].
        var results = GeneratorDriver.Run("""
            using Gener8;
            namespace External { public class Tag { public string Label { get; set; } = ""; } }
            public class Address { public string Street { get; set; } = ""; }
            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public Address ShippingAddress { get; set; } = new(); public External.Tag Category { get; set; } = new(); }
            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public AddressDto ShippingAddress", source);
        Assert.Contains("public External.Tag Category", source);
    }

    [Theory]
    [InlineData("List")]
    [InlineData("IList")]
    [InlineData("HashSet")]
    public void MapsSupportedGenericCollectionElementTypeToDto(string collectionType)
    {
        var results = GeneratorDriver.Run($$"""
            using Gener8;
            using System.Collections.Generic;
            public class Address { public string Street { get; set; } = ""; }

            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public {{collectionType}}<Address> ShippingAddresses { get; set; } = []; }

            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains($"public System.Collections.Generic.{collectionType}<AddressDto> ShippingAddresses", source);
        Assert.DoesNotContain($"public System.Collections.Generic.{collectionType}<Address> ShippingAddresses", source);
    }

    [Fact]
    public void ISetWithTypeMappingRemapsElementTypeAndConcreteContainerToHashSet()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            public class Address { public string Street { get; set; } = ""; }

            [FromModel(typeof(Address))]
            public partial class AddressDto { }
            public class Order { public ISet<Address> ShippingAddresses { get; set; } }

            [FromModel(typeof(Order))]
            [TypeMapping(typeof(Address), typeof(AddressDto))]
            public partial class OrderDto { }
            """);

        var dto = Assert.Single(results, r => r.Key == "OrderDto.g.cs").Value;
        Assert.Contains("public System.Collections.Generic.HashSet<AddressDto> ShippingAddresses", dto);
        Assert.DoesNotContain("ISet", dto);

        var ext = Assert.Single(results, r => r.Key == "OrderDtoExtensions.g.cs").Value;
        Assert.Contains("(System.Collections.Generic.HashSet<Address>)[.. dto.ShippingAddresses.Select(m => m.ToModel())]", ext);
    }

    [Fact]
    public void EmitsTypeMappingAttributeSourceFile()
    {
        var results = GeneratorDriver.Run("public class Empty { }");

        Assert.Contains("TypeMappingAttribute.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsIgnoreTypeMappingAttributeSourceFile()
    {
        var results = GeneratorDriver.Run("public class Empty { }");

        Assert.Contains("IgnoreTypeMappingAttribute.g.cs", results.Keys);
    }

    [Fact]
    public void IgnoreTypeMappingPreventsInferredMapping()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class File { public string Path { get; set; } = ""; }
            public class Document { public File Attachment { get; set; } = new(); public string Title { get; set; } = ""; }
            [FromModel(typeof(Document))]
            [IgnoreTypeMapping(typeof(File))]
            public partial class DocumentDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DocumentDto.g.cs").Value;
        Assert.Contains("public File Attachment", source);
        Assert.DoesNotContain("FileDto", source);
        Assert.DoesNotContain("FileDto.g.cs", results.Keys);
    }

    [Fact]
    public void IgnoreTypeMappingPreventsInferredCollectionMapping()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            public class File { public string Path { get; set; } = ""; }
            public class Document { public IEnumerable<File> Files { get; set; } = []; public string Title { get; set; } = ""; }
            [FromModel(typeof(Document))]
            [IgnoreTypeMapping(typeof(File))]
            public partial class DocumentDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DocumentDto.g.cs").Value;
        Assert.Contains("IEnumerable<File>", source);
        Assert.DoesNotContain("FileDto", source);
    }

    [Fact]
    public void IgnoreTypeMappingPreventsExplicitTypeMapping()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class File { public string Path { get; set; } = ""; }
            public class FileDto { public string Path { get; set; } = ""; }
            public class Document { public File Attachment { get; set; } = new(); }
            [FromModel(typeof(Document))]
            [TypeMapping(typeof(File), typeof(FileDto))]
            [IgnoreTypeMapping(typeof(File))]
            public partial class DocumentDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DocumentDto.g.cs").Value;
        Assert.Contains("public File Attachment", source);
        Assert.DoesNotContain("public FileDto", source);
    }

    [Fact]
    public void IgnoreTypeMappingPropagatesTransitivelyToAutoTargets()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            public class File { public string Path { get; set; } = ""; }
            public class Document { public IEnumerable<File> Files { get; set; } = []; public string Title { get; set; } = ""; }
            public class Folder { public IList<Document> Documents { get; set; } = []; }
            [FromModel(typeof(Folder))]
            [IgnoreTypeMapping(typeof(File))]
            public partial class FolderDto { }
            """);

        // FolderDto.Documents → IList<DocumentDto> (Document is still auto-mapped)
        var folderDto = Assert.Single(results, r => r.Key == "FolderDto.g.cs").Value;
        Assert.Contains("DocumentDto", folderDto);

        // Auto-generated DocumentDto.Files should stay as IEnumerable<File>, not IEnumerable<FileDto>
        var documentDto = Assert.Single(results, r => r.Key == "DocumentDto.g.cs").Value;
        Assert.Contains("IEnumerable<File>", documentDto);
        Assert.DoesNotContain("FileDto", documentDto);

        // FileDto should not be generated at all
        Assert.DoesNotContain("FileDto.g.cs", results.Keys);
    }

    [Fact]
    public void IgnoreTypeMappingDoesNotAffectOtherTypesInSameNamespace()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Tag { public string Label { get; set; } = ""; }
            public class File { public string Path { get; set; } = ""; }
            public class Document { public Tag Category { get; set; } = new(); public File Attachment { get; set; } = new(); }
            [FromModel(typeof(Document))]
            [IgnoreTypeMapping(typeof(File))]
            public partial class DocumentDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DocumentDto.g.cs").Value;
        Assert.Contains("TagDto", source);
        Assert.Contains("public File Attachment", source);
        Assert.DoesNotContain("FileDto", source);
    }
}
