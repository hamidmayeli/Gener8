using Amazon.DynamoDBv2.DataModel;

namespace Gener8.DynamoDb.Integration.Tests.Setup;

internal class DynamoDbRepositoryContext(IDynamoDBContext context) : IDynamoDbRepositoryContext
{
    public IDynamoDBContext Context => context;
}
