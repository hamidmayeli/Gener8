namespace FromModel.Sample;

public class Product
{
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public bool InStock { get; set; }
    public decimal Discount { get; set; }
}

[FromModel(typeof(Product))]
internal partial class ProductDto { }
