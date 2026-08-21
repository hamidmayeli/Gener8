using DynamoDb.Integration.Tests.Setup;
using DynamoDb.Integration.Tests.Setup.Models;
using Gener8;
using Microsoft.Extensions.DependencyInjection;

namespace DynamoDb.Integration.Tests;

[Collection("Shared DynamoDb Collection")]
public class TestProductRepository(TestFixture fixture) : IClassFixture<TestFixture>
{
    private readonly IRepository<Product> Repository = fixture.ServiceProvider.GetRequiredService<IRepository<Product>>();

    [Fact]
    public async Task Test1()
    {
        var product = new Product
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
        };
        await Repository.SaveAsync(product, TestContext.Current.CancellationToken);

        var retrievedProduct = await Repository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);

        Assert.Equivalent(product, retrievedProduct);

        await Repository.DeleteAsync(product, TestContext.Current.CancellationToken);

        var products = await Repository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(products);
    }
}
