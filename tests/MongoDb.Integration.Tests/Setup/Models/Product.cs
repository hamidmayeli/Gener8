using Gener8;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDb.Integration.Tests.Setup.Models;

public class Product
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Category? Category { get; set; }
    public required IReadOnlyCollection<int> Sizes { get; set; }
    public IReadOnlyCollection<Category>? Categories { get; set; }
}

[FromModel(typeof(Product), Repository = RepositoryType.MongoDb)]
public partial class ProductDto
{
    [BsonId]
    public required Guid Id { get; set; }
}
