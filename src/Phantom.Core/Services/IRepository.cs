using Phantom.Core.Domain;
using Phantom.Core.Specifications;

namespace Phantom.Core.Services;

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

public interface IWriteRepository<TId, TEntity> where TEntity : Entity<TId> where TId : notnull
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);
}

public interface IRepository<TId, TEntity> : IReadRepository<TId, TEntity>, IWriteRepository<TId, TEntity>
    where TEntity : Entity<TId>
    where TId : notnull
{
}
