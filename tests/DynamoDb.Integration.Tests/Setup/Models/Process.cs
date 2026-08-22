using Amazon.DynamoDBv2.DataModel;
using Gener8;

namespace DynamoDb.Integration.Tests.Setup.Models;

public class Process
{
    public required string Name { get; set; }

    public Guid Id { get; set; }

    public ProcessResult<ProcessStatus>? Result { get; set; }
}

public class ProcessResult<T>
{
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum ProcessStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}

[FromModel(typeof(Process), Repository = RepositoryType.DynamoDb)]
[DynamoDBTable("Processes")]
public partial class ProcessDto
{
    [DynamoDBHashKey]
    public Guid Id { get; set; }
}
