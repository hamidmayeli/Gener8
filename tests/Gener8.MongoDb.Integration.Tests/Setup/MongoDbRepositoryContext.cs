using MongoDB.Driver;

namespace Gener8.MongoDb.Integration.Tests.Setup;

internal class MongoDbRepositoryContext(IMongoDatabase context) : IMongoDbRepositoryContext
{
    public IMongoDatabase Context => context;
}
