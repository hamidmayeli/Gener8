using System.Threading;
using System.Threading.Tasks;

namespace Gener8;

public interface ICompositeKeyRepository<TModel> : IRepository<TModel> where TModel : class
{
    Task<TModel?> GetByIdAsync(object hashKey, object rangeKey, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(object hashKey, object rangeKey, CancellationToken cancellationToken = default);
}
