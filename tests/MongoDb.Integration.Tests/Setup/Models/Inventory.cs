using Gener8;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDb.Integration.Tests.Setup.Models;

public class Inventory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required IDictionary<string, Product> Products { get; set; }

    public required IDictionary<string, float> Prices { get; set; }
}

[FromModel(typeof(Inventory), Repository = RepositoryType.MongoDb)]
public partial class InventoryDto
{
    [BsonId]
    public required Guid Id { get; set; }
}
