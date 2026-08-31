using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gener8;

public abstract partial class MongoDbRepository<TModel, TDto> : IRepository<TModel>
    where TModel : class
    where TDto : class
{
    protected readonly IMongoCollection<TDto> Collection;
    protected readonly IMongoDbRepositoryContext Context;

    protected MongoDbRepository(IMongoDbRepositoryContext context, string collectionName)
    {
        Context = context;
        Collection = context.Context.GetCollection<TDto>(collectionName);
    }

    protected abstract TModel ToModel(TDto dto);
    protected abstract TDto ToDto(TModel model);

    public virtual async Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TDto>.Filter.Eq("_id", id);
        var dto = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return ToModel(dto);
    }

    public virtual async Task SaveAsync(TModel entity, CancellationToken cancellationToken = default)
    {
        var id = GetIdFromEntity(entity);
        var filter = Builders<TDto>.Filter.Eq("_id", id);
        var options = new ReplaceOptions { IsUpsert = true };

        await Collection.ReplaceOneAsync(filter, ToDto(entity), options, cancellationToken);
    }

    public virtual async Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default)
    {
        var id = GetIdFromEntity(entity);
        var filter = Builders<TDto>.Filter.Eq("_id", id);

        await Collection.DeleteOneAsync(filter, cancellationToken);
    }

    public virtual async Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TDto>.Filter.Eq("_id", id);
        await Collection.DeleteOneAsync(filter, cancellationToken);
    }

    public virtual async Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var dtos = await Collection.Find(Builders<TDto>.Filter.Empty).ToListAsync(cancellationToken);
        return dtos.Select(dto => ToModel(dto));
    }

    protected virtual object GetIdFromEntity(TModel entity)
    {
        var classMap = BsonClassMap.LookupClassMap(typeof(TModel));
        if (classMap.IdMemberMap == null)
            throw new InvalidOperationException($"No Id member found for type {typeof(TModel).Name}.");

        return classMap.IdMemberMap.Getter(entity);
    }
}
