using Amazon.DynamoDBv2.DataModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gener8;

public abstract partial class DynamoDbRepository<TModel, TDto>(IDynamoDbRepositoryContext context) : ICompositeKeyRepository<TModel>
    where TModel : class
    where TDto : class
{
    protected readonly IDynamoDbRepositoryContext Context = context;
    protected IDynamoDBContext DynamoDbContext => Context.Context;

    protected abstract TModel ToModel(TDto dto);
    protected abstract TDto ToDto(TModel model);

    public virtual async Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var dto = await DynamoDbContext.LoadAsync<TDto>(id, cancellationToken);
        return ToModel(dto);
    }

    public virtual async Task SaveAsync(TModel entity, CancellationToken cancellationToken = default)
    {
        await DynamoDbContext.SaveAsync(ToDto(entity), cancellationToken);
    }

    public virtual async Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default)
    {
        await DynamoDbContext.DeleteAsync(ToDto(entity), cancellationToken);
    }

    public virtual async Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        await DynamoDbContext.DeleteAsync<TDto>(id, cancellationToken);
    }

    public virtual async Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var search = DynamoDbContext.ScanAsync<TDto>(new List<ScanCondition>());
        return (await search.GetRemainingAsync(cancellationToken)).Select(dto => ToModel(dto));
    }

    public virtual async Task<TModel?> GetByIdAsync(object hashKey, object rangeKey, CancellationToken cancellationToken = default)
    {
        var dto = await DynamoDbContext.LoadAsync<TDto>(hashKey, rangeKey, cancellationToken);
        return ToModel(dto);
    }

    public virtual async Task DeleteByIdAsync(object hashKey, object rangeKey, CancellationToken cancellationToken = default)
    {
        await DynamoDbContext.DeleteAsync<TDto>(hashKey, rangeKey, cancellationToken);
    }
}
