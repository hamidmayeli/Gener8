using Gener8;

namespace CustomDb.Integration.Tests.Setup;

public class Product
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Category? Category { get; set; }
}

[FromModel(typeof(Product),
    Repository = RepositoryType.Custom,
    Flatten = [nameof(Product.Category)])]
public partial class ProductDto { }
