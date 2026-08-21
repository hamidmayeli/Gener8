using Amazon.DynamoDBv2.DataModel;
using MongoDB.Driver;

namespace Gener8.Sample
{
    public enum TheEnum
    {
        Value1,
        Value2,
    }

    public class DynamoDbContent
    {
        public string? Key { get; set; }
        public TheEnum EnumValue { get; set; }
        public TheEnum? NullabeEnumValue { get; set; }
    }

    [FromModel(typeof(DynamoDbContent), Repository = RepositoryType.DynamoDb)]
    public partial class DynamoDbContentDto { }

    public class MongoDbContent
    {
        public string? Key { get; set; }
        public TheEnum EnumValue { get; set; }
    }

    [FromModel(typeof(MongoDbContent), Repository = RepositoryType.MongoDb)]
    public partial class MongoDbContentDto { }

    public class DynamoDbRepositoryContext(IDynamoDBContext context) : IDynamoDbRepositoryContext
    {
        public IDynamoDBContext Context => context;
    }

    public class MongoDbRepositoryContext(IMongoDatabase context) : IMongoDbRepositoryContext
    {
        public IMongoDatabase Context => context;
    }

    public class CustomRepositoryContent
    {
        public string? Key { get; set; }
    }

    [FromModel(typeof(CustomRepositoryContent), Repository = RepositoryType.Custom)]
    public partial class CustomRepositoryContentDto { }
}

namespace Gener8
{
    partial class RepositoryBase<TModel, TDto>
    {
        public Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SaveAsync(TModel entity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}