using Phantom.Core.Domain;
using Phantom.Core.Specifications;

namespace Phantom.Core.Services;

/// <summary>
/// Read-side of the Repository abstraction. Exposes only queries — no mutations.
///
/// Splitting the read side from the write side follows the DDD convention that repositories
/// are a means to retrieve aggregates (not a generic collection-like abstraction over the
/// database). Consumers that only need to read should depend on <see cref="IReadRepository{TId,TEntity}"/>
/// so the type system enforces that they cannot accidentally mutate state.
/// </summary>
public interface IReadRepository<TId, TEntity> where TEntity : Entity<TId> where TId : notnull
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<TEntity?> GetByIdAsNoTrackingAsync(TId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> FindAsNoTrackingAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    Task<long> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(TId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Write-side of the Repository abstraction. Exposes only mutations — no queries.
/// </summary>
public interface IWriteRepository<TId, TEntity> where TEntity : Entity<TId> where TId : notnull
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);
}

/// <summary>
/// Combined read + write repository. Prefer depending on <see cref="IReadRepository{TId,TEntity}"/>
/// or <see cref="IWriteRepository{TId,TEntity}"/> directly when you only need one side — this
/// interface exists for backward compatibility and for handlers that genuinely need both.
///
/// Note: <c>GetAllAsync</c> and <c>GetAllAsNoTrackingAsync</c> are intentionally NOT on this
/// interface. Returning the entire table without a specification is an anti-pattern. Use
/// <see cref="FindAsync"/> with an empty specification if you really need every row.
/// </summary>
public interface IRepository<TId, TEntity> : IReadRepository<TId, TEntity>, IWriteRepository<TId, TEntity>
    where TEntity : Entity<TId>
    where TId : notnull
{
}
