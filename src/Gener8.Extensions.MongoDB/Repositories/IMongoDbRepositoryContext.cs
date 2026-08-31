using MongoDB.Driver;

namespace Gener8;

public interface IMongoDbRepositoryContext
{
    IMongoDatabase Context { get; }
}
