using Gener8;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace CustomDb.Integration.Tests.Setup;

public class TestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer;
    private IServiceProvider? _serviceProvider;

    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Service provider is not initialized.");

    public TestFixture()
    {
        _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var services = new ServiceCollection();

        var connectionString = GetConnectionString();

        services
            .AddSingleton<IRepositoryContext>(new RepositoryContext(connectionString))
            .AddTransient<IRepository<Product>, ProductRepository>();

        _serviceProvider = services.BuildServiceProvider();

        await CreateTestTableAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _msSqlContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private string GetConnectionString()
    {
        var rawConnectionString = _msSqlContainer.GetConnectionString();
        var builder = new SqlConnectionStringBuilder(rawConnectionString)
        {
            // Force TrustServerCertificate to prevent SSL handshake drops
            TrustServerCertificate = true,
        };

        builder.DataSource = builder.DataSource.Replace("localhost", "127.0.0.1");

        return builder.ConnectionString;
    }

    private async Task CreateTestTableAsync()
    {
        var connectionString = GetConnectionString();
        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);

                // This is where it was timing out. The retry loop will catch it.
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE Products (
                        Id UNIQUEIDENTIFIER PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        Description NVARCHAR(255) NULL,
                        CategoryName NVARCHAR(100) NULL
                    );";

                await command.ExecuteNonQueryAsync();

                // If we succeed, exit the method completely
                return;
            }
            catch (SqlException ex)
            {
                // If it's the last attempt, throw the exception to fail the test
                if (i == maxRetries - 1)
                {
                    throw new Exception($"Failed to connect to SQL Server after {maxRetries} attempts. Last error: {ex.Message}", ex);
                }

                // Otherwise, wait 2 seconds and try again
                await Task.Delay(delay);
            }
        }
    }
}
