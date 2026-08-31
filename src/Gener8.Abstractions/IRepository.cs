using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gener8;

public interface IRepository<TModel> where TModel : class
{
    Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task SaveAsync(TModel entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default);
}
