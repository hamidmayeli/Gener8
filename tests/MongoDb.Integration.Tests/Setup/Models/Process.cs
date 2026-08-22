using Gener8;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDb.Integration.Tests.Setup.Models;

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

[FromModel(typeof(Process), Repository = RepositoryType.MongoDb)]
public partial class ProcessDto
{
    [BsonId]
    public Guid Id { get; set; }
}
