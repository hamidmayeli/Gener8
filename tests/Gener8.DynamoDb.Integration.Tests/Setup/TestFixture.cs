using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.DynamoDb;

namespace Gener8.DynamoDb.Integration.Tests.Setup;

public class TestFixture : IAsyncLifetime
{
    private readonly DynamoDbContainer _dynamoDbContainer;
    private IServiceProvider? _serviceProvider;

    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Service provider is not initialized.");

    public TestFixture()
    {
        _dynamoDbContainer = new DynamoDbBuilder("amazon/dynamodb-local:latest")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _dynamoDbContainer.StartAsync();

        // 1. Force the SDK to stop trying to discover real AWS endpoints
        Environment.SetEnvironmentVariable("AWS_ENABLE_ENDPOINT_DISCOVERY", "false");

        // 2. Scrub any real ambient AWS credentials floating in your developer environment
        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", null);
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);

        var services = new ServiceCollection();
        var serviceUrl = _dynamoDbContainer.GetConnectionString();

        services
            .AddSingleton<IAmazonDynamoDB>(sp =>
            {
                var config = new AmazonDynamoDBConfig
                {
                    ServiceURL = serviceUrl,
                    AuthenticationRegion = "us-east-1"
                };

                // 3. Use purely alphanumeric fake keys. NO hyphens.
                var credentials = new BasicAWSCredentials("dummy", "dummy");

                return new AmazonDynamoDBClient(credentials, config);
            })
            .AddSingleton<IDynamoDBContext, DynamoDBContext>()
            .AddTransient<IRepository<Product>, ProductRepository>()
            .AddTransient<IDynamoDbRepositoryContext, DynamoDbRepositoryContext>();

        _serviceProvider = services.BuildServiceProvider();

        await CreateTestTableAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dynamoDbContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    private async Task CreateTestTableAsync()
    {
        // Resolve the client we just registered
        var dynamoDbClient = ServiceProvider.GetRequiredService<IAmazonDynamoDB>();

        var request = new CreateTableRequest
        {
            TableName = "Products",
            AttributeDefinitions =
            [
                new AttributeDefinition("Id", ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement("Id", KeyType.HASH)
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await dynamoDbClient.CreateTableAsync(request);
    }
}
