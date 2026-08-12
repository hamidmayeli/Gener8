namespace FromModel.Sample;

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
}

[FromModel(typeof(Order), Flatten = [nameof(Order.ShippingAddress)])]
[TypeMapping(typeof(Customer), typeof(CustomerDto))]
public partial class OrderDto { }

[FromModel(typeof(Customer))]
public partial class CustomerDto { }
