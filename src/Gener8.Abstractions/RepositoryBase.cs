using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gener8;

public abstract class RepositoryBase<TModel, TDto> : IRepository<TModel>
    where TModel : class
    where TDto : class
{
    protected readonly IRepositoryContext Context;

    protected RepositoryBase(IRepositoryContext context) => Context = context;

    protected abstract TModel ToModel(TDto dto);
    protected abstract TDto ToDto(TModel model);

    // IRepository<TModel> CRUD contract — concrete subclass provides the implementations.
    // The generated partial class adds the body in a second partial file written by the consumer.
    public abstract Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    public abstract Task SaveAsync(TModel entity, CancellationToken cancellationToken = default);
    public abstract Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default);
    public abstract Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default);
    public abstract Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default);
}
