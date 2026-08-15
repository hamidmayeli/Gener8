using Amazon.DynamoDBv2.DataModel;

namespace Gener8.DynamoDb.Integration.Tests.Setup;

public class Product
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Category? Category { get; set; }
}

[FromModel(typeof(Product), Repository = RepositoryType.DynamoDb)]
[DynamoDBTable("Products")]
public partial class ProductDto
{
    [DynamoDBHashKey]
    public required Guid Id { get; set; }
}
