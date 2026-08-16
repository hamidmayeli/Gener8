using Gener8;
using Microsoft.Extensions.DependencyInjection;
using MongoDb.Integration.Tests.Setup;

namespace MongoDb.Integration.Tests;

[Collection("Shared DynamoDb Collection")]
public class UnitTest1(TestFixture fixture) : IClassFixture<TestFixture>
{
    private readonly IRepository<Product> Repository = fixture.ServiceProvider.GetRequiredService<IRepository<Product>>();

    [Fact]
    public async Task Test1()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Category = new() { Name = "Test Category" },
            Description = "Test Description",
        };
        await Repository.SaveAsync(product, TestContext.Current.CancellationToken);

        var retrievedProduct = await Repository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(retrievedProduct);

        await Repository.DeleteAsync(product, TestContext.Current.CancellationToken);

        var products = await Repository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(products);
    }
}
