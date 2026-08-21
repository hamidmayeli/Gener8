namespace Gener8.Tests;

public class RepositoryTests
{
    private const string ProductModel = """
        using Gener8;
        public class Product { public string Name { get; set; } = ""; }
        """;

    // ---- Default (no repository) ----

    [Fact]
    public void NoRepositoryFileEmittedByDefault()
    {
        // Run with full compilation check — proves no SDK-type errors leak when Repository is not used.
        var results = GeneratorDriver.Run(ProductModel + """
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        Assert.DoesNotContain(results.Keys, k => k.EndsWith("Repository.g.cs") && !k.StartsWith("I"));
    }

    // ---- DynamoDB ----

    [Fact]
    public void EmitsDynamoDbRepositoryFile()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductRepository.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsDynamoDbBaseClassFile()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("DynamoDbRepository.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsNullableEnumStringConverter()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("NullableEnumToStringConverter.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsEnumStringConverter()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("EnumToStringConverter.g.cs", results.Keys);
    }

    [Fact]
    public void DynamoDbRepositoryHasCorrectClassName()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("class ProductRepository", source);
    }

    [Fact]
    public void DynamoDbRepositoryInheritsFromBase()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("Gener8.DynamoDbRepository<", source);
        Assert.Contains("ProductDto>", source);
    }

    [Fact]
    public void DynamoDbRepositoryConstructorTakesDynamoDbRepositoryContext()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("IDynamoDbRepositoryContext context", source);
        Assert.Contains(": base(context)", source);
    }

    [Fact]
    public void DynamoDbRepositoryOverridesToModelAndToDto()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("ToModel(ProductDto dto) => dto.ToModel()", source);
        Assert.Contains("ToDto(", source);
        Assert.Contains("model) => model.ToDto()", source);
    }

    [Fact]
    public void DynamoDbRepositoryHintNameIncludesNamespace()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            namespace My.App
            {
                [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
                public partial class ProductDto { }
            }
            """);

        Assert.Contains("My.App.ProductRepository.g.cs", results.Keys);
    }

    [Fact]
    public void DynamoDbRepositoryAccessibilityMatchesPublicDto()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("public partial class ProductRepository", source);
    }

    [Theory]
    [InlineData("CategoryEnum", "EnumToStringConverter")]
    [InlineData("CategoryEnum?", "NullableEnumToStringConverter")]
    public void EnumPropertyHasCorrectConverterInDynamoDb(string propertyType, string converterType)
    {
        var results = GeneratorDriver.RunUnchecked($$"""
            using Gener8;
            public enum CategoryEnum { A, B, C }
            public class Product { public {{propertyType}} Category { get; set; } }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);
        var source = results["ProductDto.g.cs"];
        Assert.Contains($"[DynamoDBProperty(typeof({converterType}<CategoryEnum>))]", source);
    }

    [Theory]
    [InlineData("IList<CategoryEnum>", "EnumListToStringListConverter")]
    [InlineData("IList<CategoryEnum?>", "NullableEnumListToStringListConverter")]
    public void CollectionEnumPropertyHasCorrectConverterInDynamoDb(string propertyType, string converterType)
    {
        var results = GeneratorDriver.RunUnchecked($$"""
            using Gener8;
            using System.Collections.Generic;
            public enum CategoryEnum { A, B, C }
            public class Product { public {{propertyType}} Category { get; set; } }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);
        var source = results["ProductDto.g.cs"];
        Assert.Contains($"[DynamoDBProperty(typeof({converterType}<CategoryEnum>))]", source);
    }

    [Fact]
    public void NestedEmumPropertyHasConverterInDynamoDb()
    {
        var results = GeneratorDriver.RunUnchecked("""
            using Gener8;
            public enum CategoryEnum { A, B, C }
            public class Product { public CategoryEnum Category { get; set; } }
            public class WishListItem { public Product Product { get; set; } public DateTime By { get; set; } }
            [FromModel(typeof(WishListItem), Repository = RepositoryType.DynamoDb)]
            public partial class WishListItemDto { }
            """);
        var source = results["ProductDto.g.cs"];
        Assert.Contains($"[DynamoDBProperty(typeof(EnumToStringConverter<CategoryEnum>))]", source);
    }

    [Fact]
    public void DynamoDbRepositoryAccessibilityMatchesInternalDto()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            internal partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("internal partial class ProductRepository", source);
    }

    [Fact]
    public void DtoAndExtensionsFilesStillEmittedAlongsideDynamoDbRepository()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDto.g.cs", results.Keys);
        Assert.Contains("ProductDtoExtensions.g.cs", results.Keys);
        Assert.Contains("ProductRepository.g.cs", results.Keys);
    }

    [Fact]
    public void DynamoDbBaseClassEmittedOnlyOnceForMultipleDtos()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            public class Order { public int Id { get; set; } }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            [FromModel(typeof(Order), Repository = RepositoryType.DynamoDb)]
            public partial class OrderDto { }
            """);

        var dynamo = results.Keys.Where(k => k == "DynamoDbRepository.g.cs").ToList();
        Assert.Single(dynamo);
    }

    // ---- MongoDB ----

    [Fact]
    public void EmitsMongoDbRepositoryFile()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductRepository.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsMongoDbBaseClassFile()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("MongoDbRepository.g.cs", results.Keys);
    }

    [Fact]
    public void MongoDbRepositoryInheritsFromBase()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("Gener8.MongoDbRepository<", source);
        Assert.Contains("ProductDto>", source);
    }

    [Fact]
    public void MongoDbRepositoryConstructorTakesIMongoDbRepositoryContext()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("IMongoDbRepositoryContext context", source);
        Assert.Contains(": base(context,", source);
    }

    [Fact]
    public void MongoDbRepositoryOverridesToModelAndToDto()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductRepository.g.cs"];
        Assert.Contains("ToModel(ProductDto dto) => dto.ToModel()", source);
        Assert.Contains("model) => model.ToDto()", source);
    }

    [Fact]
    public void MongoDbRepositoryHintNameIncludesNamespace()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            namespace My.App
            {
                [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
                public partial class ProductDto { }
            }
            """);

        Assert.Contains("My.App.ProductRepository.g.cs", results.Keys);
    }

    [Fact]
    public void MongoDbBaseClassEmittedOnlyOnceForMultipleDtos()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            public class Order { public int Id { get; set; } }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            [FromModel(typeof(Order), Repository = RepositoryType.MongoDb)]
            public partial class OrderDto { }
            """);

        var mongo = results.Keys.Where(k => k == "MongoDbRepository.g.cs").ToList();
        Assert.Single(mongo);
    }

    [Fact]
    public void EnumPropertyHasCorrectConverterInMongoDb()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            public enum CategoryEnum { A, B, C }
            public class Product { public CategoryEnum Category { get; set; } }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDto.g.cs"];
        Assert.Contains("[BsonRepresentation(BsonType.String)]", source);
    }

    [Fact]
    public void DtoAndExtensionsFilesStillEmittedAlongsideMongoDbRepository()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDto.g.cs", results.Keys);
        Assert.Contains("ProductDtoExtensions.g.cs", results.Keys);
        Assert.Contains("ProductRepository.g.cs", results.Keys);
    }

    // ---- Custom ----

    [Fact]
    public void EmitsCustomRepositoryFile()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.Custom)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductRepository.g.cs", results.Keys);
    }

    [Fact]
    public void EmitsCustomBaseClassFile()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            [FromModel(typeof(Product), Repository = RepositoryType.Custom)]
            public partial class ProductDto { }
            """);

        Assert.Contains("CustomRepository.g.cs", results.Keys);
    }

    // ---- Mixed (both kinds in the same compilation) ----

    [Fact]
    public void BothBaseClassesEmittedWhenBothKindsUsed()
    {
        var results = GeneratorDriver.RunUnchecked(ProductModel + """
            public class Order { public int Id { get; set; } }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            [FromModel(typeof(Order), Repository = RepositoryType.MongoDb)]
            public partial class OrderDto { }
            """);

        Assert.Contains("DynamoDbRepository.g.cs", results.Keys);
        Assert.Contains("MongoDbRepository.g.cs", results.Keys);
    }

    // ---- Abstract collection remapping (DynamoDB cannot instantiate interfaces) ----

    [Fact]
    public void DynamoDb_AbstractCollectionRemappedToList()
    {
        var results = GeneratorDriver.RunUnchecked("""
            using Gener8;
            using System.Collections.Generic;
            public class Product { public IReadOnlyCollection<int> Sizes { get; set; } = []; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDto.g.cs"];
        Assert.Contains("System.Collections.Generic.List<int>", source);
        Assert.DoesNotContain("IReadOnlyCollection", source);
    }

    [Fact]
    public void DynamoDb_AbstractCollectionUsesSpreadInToDto()
    {
        var results = GeneratorDriver.RunUnchecked("""
            using Gener8;
            using System.Collections.Generic;
            public class Product { public IReadOnlyCollection<int> Sizes { get; set; } = []; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("[.. model.Sizes]", source);
    }

    [Fact]
    public void DynamoDb_NonRepositoryDtoKeepsAbstractCollection()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            using System.Collections.Generic;
            public class Product { public IReadOnlyCollection<int> Sizes { get; set; } = []; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        var source = results["ProductDto.g.cs"];
        Assert.Contains("IReadOnlyCollection", source);
        Assert.DoesNotContain("List<int>", source);
    }
}
