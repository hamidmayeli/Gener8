namespace Gener8.Sample;

public class Order
{
    public int Id { get; set; }
    public Address? ShippingAddress { get; set; }
    public required Customer Customer { get; set; }
}

public class Address
{
    public required string Street { get; set; }
    public required string City { get; set; }
    public string? State { get; set; }
}

public class Customer
{
    public required string Name { get; set; }
    public required CustomerType Type { get; set; }
}

public enum CustomerType
{
    Gold,
    Silver,
}

[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
public partial class OrderDto { }
