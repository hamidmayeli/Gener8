namespace Gener8.Tests;

public class RepositoryTests
{
    private const string DynamoDbStubs = """
        namespace Amazon.DynamoDBv2
        {
            public interface IAmazonDynamoDB { }
        }
        """;

    private const string MongoDbStubs = """
        namespace MongoDB.Driver
        {
            public interface IMongoCollection<T> { }
            public interface IMongoDatabase { IMongoCollection<T> GetCollection<T>(string name); }
            public interface IMongoClient { IMongoDatabase GetDatabase(string name); }
        }
        """;

    // ---- Default (no repository) ----

    [Fact]
    public void NoRepositoryFileEmittedByDefault()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product))]
            public partial class ProductDto { }
            """);

        Assert.DoesNotContain(results.Keys, k => k.EndsWith("DbRepository.g.cs"));
    }

    // ---- DynamoDB ----

    [Fact]
    public void EmitsDynamoDbRepositoryFile()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDtoDynamoDbRepository.g.cs", results.Keys);
    }

    [Fact]
    public void DynamoDbRepositoryHasCorrectClassName()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("class ProductDtoDynamoDbRepository", source);
    }

    [Fact]
    public void DynamoDbRepositoryInheritsFromBase()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("global::Gener8.Repository<ProductDto>", source);
    }

    [Fact]
    public void DynamoDbRepositoryHasClientField()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("global::Amazon.DynamoDBv2.IAmazonDynamoDB _client;", source);
    }

    [Fact]
    public void DynamoDbRepositoryHasSettingsField()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("global::Gener8.DynamoDbRepositorySettings _settings;", source);
    }

    [Fact]
    public void DynamoDbRepositoryConstructorAssignsBothFields()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("_client = client;", source);
        Assert.Contains("_settings = settings;", source);
    }

    [Fact]
    public void DynamoDbRepositoryHasAllFourMethodStubs()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("GetByIdAsync(", source);
        Assert.Contains("GetAllAsync(", source);
        Assert.Contains("SaveAsync(", source);
        Assert.Contains("DeleteAsync(", source);
    }

    [Fact]
    public void DynamoDbRepositoryMethodsThrowNotImplementedException()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Equal(4, source.Split("global::System.NotImplementedException()").Length - 1);
    }

    [Fact]
    public void DynamoDbRepositoryHintNameIncludesNamespace()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            namespace My.App
            {
                [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
                public partial class ProductDto { }
            }
            """);

        Assert.Contains("My.App.ProductDtoDynamoDbRepository.g.cs", results.Keys);
    }

    [Fact]
    public void DynamoDbRepositoryAccessibilityMatchesPublicDto()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("public class ProductDtoDynamoDbRepository", source);
    }

    [Fact]
    public void DynamoDbRepositoryAccessibilityMatchesInternalDto()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            internal partial class ProductDto { }
            """);

        var source = results["ProductDtoDynamoDbRepository.g.cs"];
        Assert.Contains("internal class ProductDtoDynamoDbRepository", source);
    }

    [Fact]
    public void DtoAndExtensionsFilesStillEmittedAlongsideDynamoDbRepository()
    {
        var results = GeneratorDriver.Run(DynamoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDto.g.cs", results.Keys);
        Assert.Contains("ProductDtoExtensions.g.cs", results.Keys);
        Assert.Contains("ProductDtoDynamoDbRepository.g.cs", results.Keys);
    }

    // ---- MongoDB ----

    [Fact]
    public void EmitsMongoDbRepositoryFile()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDtoMongoDbRepository.g.cs", results.Keys);
    }

    [Fact]
    public void MongoDbRepositoryHasCorrectClassName()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoMongoDbRepository.g.cs"];
        Assert.Contains("class ProductDtoMongoDbRepository", source);
    }

    [Fact]
    public void MongoDbRepositoryInheritsFromBase()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoMongoDbRepository.g.cs"];
        Assert.Contains("global::Gener8.Repository<ProductDto>", source);
    }

    [Fact]
    public void MongoDbRepositoryHasCollectionField()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoMongoDbRepository.g.cs"];
        Assert.Contains("global::MongoDB.Driver.IMongoCollection<ProductDto> _collection;", source);
    }

    [Fact]
    public void MongoDbRepositoryConstructorResolvesCollectionFromClient()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoMongoDbRepository.g.cs"];
        Assert.Contains("client.GetDatabase(settings.DatabaseName).GetCollection<ProductDto>(settings.CollectionName)", source);
    }

    [Fact]
    public void MongoDbRepositoryHasAllFourMethodStubs()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoMongoDbRepository.g.cs"];
        Assert.Contains("GetByIdAsync(", source);
        Assert.Contains("GetAllAsync(", source);
        Assert.Contains("SaveAsync(", source);
        Assert.Contains("DeleteAsync(", source);
    }

    [Fact]
    public void MongoDbRepositoryMethodsThrowNotImplementedException()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        var source = results["ProductDtoMongoDbRepository.g.cs"];
        Assert.Equal(4, source.Split("global::System.NotImplementedException()").Length - 1);
    }

    [Fact]
    public void MongoDbRepositoryHintNameIncludesNamespace()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            namespace My.App
            {
                [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
                public partial class ProductDto { }
            }
            """);

        Assert.Contains("My.App.ProductDtoMongoDbRepository.g.cs", results.Keys);
    }

    [Fact]
    public void DtoAndExtensionsFilesStillEmittedAlongsideMongoDbRepository()
    {
        var results = GeneratorDriver.Run(MongoDbStubs, """
            using Gener8;
            public class Product { public string Name { get; set; } = ""; }
            [FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
            public partial class ProductDto { }
            """);

        Assert.Contains("ProductDto.g.cs", results.Keys);
        Assert.Contains("ProductDtoExtensions.g.cs", results.Keys);
        Assert.Contains("ProductDtoMongoDbRepository.g.cs", results.Keys);
    }
}
