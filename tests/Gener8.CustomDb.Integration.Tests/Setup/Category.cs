namespace Gener8.CustomDb.Integration.Tests.Setup;

public class Category
{
    public required string Name { get; set; }
}

[FromModel(typeof(Category))]
public partial class CategoryDto { }
