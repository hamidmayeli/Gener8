using Amazon.DynamoDBv2.DataModel;

namespace Gener8;

public interface IDynamoDbRepositoryContext
{
    IDynamoDBContext Context { get; }
}
