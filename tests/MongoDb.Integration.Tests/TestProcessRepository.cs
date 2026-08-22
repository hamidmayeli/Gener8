using Gener8;
using Microsoft.Extensions.DependencyInjection;
using MongoDb.Integration.Tests.Setup;
using MongoDb.Integration.Tests.Setup.Models;

namespace MongoDb.Integration.Tests;


[Collection("Shared DynamoDb Collection")]
public class TestProcessRepository(TestFixture fixture) : IClassFixture<TestFixture>
{
    private readonly IRepository<Process> Repository = fixture.ServiceProvider.GetRequiredService<IRepository<Process>>();

    [Fact]
    public async Task Test1()
    {
        var process = new Process
        {
            Name = "Test Process",
            Id = Guid.NewGuid(),
            Result = new ProcessResult<ProcessStatus>
            {
                Data = ProcessStatus.Pending,
                ErrorMessage = null
            }
        };
        await Repository.SaveAsync(process, TestContext.Current.CancellationToken);
        var retrievedProcess = await Repository.GetByIdAsync(process.Id, TestContext.Current.CancellationToken);
        Assert.Equivalent(process, retrievedProcess);
        await Repository.DeleteAsync(process, TestContext.Current.CancellationToken);
        var processes = await Repository.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Empty(processes);
    }
}
