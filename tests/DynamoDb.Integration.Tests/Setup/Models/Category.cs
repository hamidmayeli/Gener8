using Gener8;

namespace DynamoDb.Integration.Tests.Setup.Models;

public class Category
{
    public required string Name { get; set; }
    public CategoryGroup Group { get; set; }
}

[FromModel(typeof(Category))]
public partial class CategoryDto { }

public enum CategoryGroup
{
    Primary,
    Secondary,
}
