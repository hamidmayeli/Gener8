using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Gener8.MongoDb.Integration.Tests.Setup;

public class TestFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoDbContainer;
    private IServiceProvider? _serviceProvider;

    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Service provider is not initialized.");

    static TestFixture()
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }

    public TestFixture()
    {
        _mongoDbContainer = new MongoDbBuilder("mongo:latest")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _mongoDbContainer.StartAsync();

        var services = new ServiceCollection();
        var mongoUrl = _mongoDbContainer.GetConnectionString();

        services
            .AddSingleton(sp => new MongoClient(mongoUrl))
            .AddTransient(sp =>
            {
                var client = sp.GetRequiredService<MongoClient>();
                // MongoDB automatically creates the database on the first write
                return client.GetDatabase("ProductDto");
            })
            .AddTransient<IRepository<Product>, ProductRepository>()
            .AddTransient<IMongoDbRepositoryContext, MongoDbRepositoryContext>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _mongoDbContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
