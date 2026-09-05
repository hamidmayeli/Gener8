using Amazon.DynamoDBv2.DataModel;
using Gener8;

namespace DynamoDb.Integration.Tests.Setup.Models;

public class Inventory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required IDictionary<string, Product> Products { get; set; }

    public required IDictionary<string, float> Prices { get; set; }
}

[DynamoDBTable("Inventories")]
[FromModel(typeof(Inventory), Repository = RepositoryType.DynamoDb)]
public partial class InventoryDto
{
    [DynamoDBHashKey]
    public required Guid Id { get; set; }
}
