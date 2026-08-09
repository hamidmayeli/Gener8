using FromModel;

var dto = new ProductDto { Name = "Widget", Price = 9.99m, InStock = true };
Console.WriteLine($"{dto.Name} — £{dto.Price} — {(dto.InStock ? "in stock" : "out of stock")}");

[FromModel(nameof(Product))]
internal partial class ProductDto { }
