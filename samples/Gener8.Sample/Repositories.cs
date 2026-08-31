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

namespace Gener8.Sample
{
    public partial class CustomRepositoryContentRepository
    {
        public override Task<CustomRepositoryContent?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task SaveAsync(CustomRepositoryContent entity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task DeleteAsync(CustomRepositoryContent entity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task<IEnumerable<CustomRepositoryContent>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}