using FromModel;

var dto = new ProductDto { Name = "Widget", Price = 9.99m, InStock = true, Discount = 1.00m };
Console.WriteLine($"{dto.Name} — £{dto.Price} — {(dto.InStock ? "in stock" : "out of stock")} — Discount: £{dto.Discount}");

[FromModel(typeof(Product))]
internal partial class ProductDto { }
