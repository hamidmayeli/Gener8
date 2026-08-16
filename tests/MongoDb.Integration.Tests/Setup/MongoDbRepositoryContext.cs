using Gener8;
using MongoDB.Driver;

namespace MongoDb.Integration.Tests.Setup;

internal class MongoDbRepositoryContext(IMongoDatabase context) : IMongoDbRepositoryContext
{
    public IMongoDatabase Context => context;
}
