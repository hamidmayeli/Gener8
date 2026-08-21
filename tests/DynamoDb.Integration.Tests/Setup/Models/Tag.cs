namespace DynamoDb.Integration.Tests.Setup.Models;

public record Tag(string Name)
{
    public int Order { get; set; }
}
