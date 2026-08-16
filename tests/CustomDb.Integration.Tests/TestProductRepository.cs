using CustomDb.Integration.Tests.Setup;
using Gener8;
using Microsoft.Extensions.DependencyInjection;

namespace CustomDb.Integration.Tests;

[Collection("Shared CustomDb Collection")]
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
            Category = new()
            {
                Name = "Test Category",
                Description = "Test Category Description",
            },
            Description = "Test Description",
            Sizes = [1, 2, 3],
        };
        await Repository.SaveAsync(product, TestContext.Current.CancellationToken);

        var retrievedProduct = await Repository.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);

        Assert.Equivalent(product, retrievedProduct);

        await Repository.DeleteAsync(product, TestContext.Current.CancellationToken);

        var products = await Repository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(products);
    }
}
