namespace DynamoDb.Integration.Tests.Setup.Models;

public class Category
{
    public required string Name { get; set; }
    public CategoryGroup Group { get; set; }
}

public enum CategoryGroup
{
    Primary,
    Secondary,
}
