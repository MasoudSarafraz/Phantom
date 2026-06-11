using Phantom.Core.Domain;
using System.Linq.Expressions;

namespace Phantom.Core.Repositories;


public interface IRepository<TAggregate, TId> where TAggregate : AggregateRoot<TId>
{
    Task<TAggregate> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TAggregate>> FindAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TAggregate>> GetAllAsync(CancellationToken cancellationToken = default);
}