namespace Gener8.MongoDb.Integration.Tests.Setup;

public class Category
{
    public required string Name { get; set; }
}

[FromModel(typeof(Category))]
public partial class CategoryDto { }
