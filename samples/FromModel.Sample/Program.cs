using System.Text.Json;
using FromModel.Sample;

var dto = new ProductDto { Name = "Widget", Price = 9.99m, InStock = true, Discount = 1.00m };
Console.WriteLine($"{dto.Name} — £{dto.Price} — {(dto.InStock ? "in stock" : "out of stock")} — Discount: £{dto.Discount}");

var orderDto = new Order
{
    Customer = new Customer { Name = "John Doe" },
    Id = 123,
    ShippingAddress = new()
    {
        Street = "123 Main St",
        City = "Anytown",
    }
}.ToDto();

Console.WriteLine(JsonSerializer.Serialize(orderDto));
