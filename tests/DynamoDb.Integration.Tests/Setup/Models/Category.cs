using Gener8;
using System.Text.RegularExpressions;

namespace DynamoDb.Integration.Tests.Setup;

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
