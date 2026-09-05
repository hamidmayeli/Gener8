using DynamoDb.Integration.Tests.Setup;
using DynamoDb.Integration.Tests.Setup.Models;
using Gener8;
using Microsoft.Extensions.DependencyInjection;

namespace DynamoDb.Integration.Tests;

[Collection("Shared DynamoDb Collection")]
public class TestProductRepository(TestFixture fixture) : IClassFixture<TestFixture>
{
    private readonly IRepository<Product> ProductRepository = fixture.ServiceProvider.GetRequiredService<IRepository<Product>>();
    private readonly IRepository<Inventory> InventoryRepository = fixture.ServiceProvider.GetRequiredService<IRepository<Inventory>>();

    [Fact]
    public async Task TestProducts()
    {
        var product = CreateProduct();

        await ProductRepository.SaveAsync(product, TestContext.Current.CancellationToken);

        var retrievedProduct = await ProductRepository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);

        Assert.Equivalent(product, retrievedProduct);

        await ProductRepository.DeleteAsync(product, TestContext.Current.CancellationToken);

        var products = await ProductRepository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(products);
    }

    [Fact]
    public async Task TestInventory()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            Products = new Dictionary<string, Product>
            {
                { "prod1", CreateProduct() },
                { "prod2", CreateProduct() },
            },
            Prices = new Dictionary<string, float>
            {
                { "prod1", 9.99f },
                { "prod2", 19.99f },
            }
        };

        await InventoryRepository.SaveAsync(inventory, TestContext.Current.CancellationToken);

        var retrievedInventory = await InventoryRepository.GetByIdAsync(inventory.Id, TestContext.Current.CancellationToken);

        Assert.Equivalent(inventory, retrievedInventory);

        await InventoryRepository.DeleteAsync(inventory, TestContext.Current.CancellationToken);

        var inventories = await InventoryRepository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(inventories);
    }

    private static Product CreateProduct()
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Category = new() { Name = "Test Category", Group = CategoryGroup.Primary },
            Description = "Test Description",
            Sizes = [1, 2, 3],
            Categories = [
                new() { Name = "Category 1", Group = CategoryGroup.Secondary },
                new() { Name = "Category 2", Group = CategoryGroup.Secondary },
            ],
            Tag = new("Sample") { Order = 1 },
        };
    }
}
