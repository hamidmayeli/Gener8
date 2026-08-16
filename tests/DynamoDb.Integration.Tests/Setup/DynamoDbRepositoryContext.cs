using Amazon.DynamoDBv2.DataModel;
using Gener8;

namespace DynamoDb.Integration.Tests.Setup;

internal class DynamoDbRepositoryContext(IDynamoDBContext context) : IDynamoDbRepositoryContext
{
    public IDynamoDBContext Context => context;
}
