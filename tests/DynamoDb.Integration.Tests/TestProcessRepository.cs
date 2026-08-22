using DynamoDb.Integration.Tests.Setup;
using DynamoDb.Integration.Tests.Setup.Models;
using Gener8;
using Microsoft.Extensions.DependencyInjection;

namespace DynamoDb.Integration.Tests;


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
