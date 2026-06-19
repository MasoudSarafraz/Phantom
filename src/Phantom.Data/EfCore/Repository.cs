using Microsoft.EntityFrameworkCore;
using Phantom.Core.Domain;
using Phantom.Core.Services;
using Phantom.Core.Specifications;
using Phantom.Data.Specifications;

namespace Phantom.Data.EfCore;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TId, TEntity}"/>.
///
/// Implements both <see cref="IReadRepository{TId, TEntity}"/> and <see cref="IWriteRepository{TId, TEntity}"/>.
/// The legacy <c>GetAllAsync</c> / <c>GetAllAsNoTrackingAsync</c> methods remain on the concrete
/// class for callers that explicitly want them, but they are NOT on the public interfaces —
/// consumers should normally use <see cref="FindAsync"/> with a specification.
/// </summary>
public class Repository<TId, TEntity> : IRepository<TId, TEntity>
    where TEntity : Entity<TId>
    where TId : notnull
{
    private readonly PhantomDbContext _dbContext;
    private readonly ISpecificationEvaluator _specificationEvaluator;

    public Repository(PhantomDbContext dbContext, ISpecificationEvaluator specificationEvaluator)
    {
        _dbContext = dbContext;
        _specificationEvaluator = specificationEvaluator;
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().FindAsync(new object[] { id! }, cancellationToken);

    public async Task<TEntity?> GetByIdAsNoTrackingAsync(TId id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<TId>(e, "Id")!.Equals(id!), cancellationToken);

    public async Task<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
        => await _specificationEvaluator.ApplySpecification(_dbContext.Set<TEntity>(), specification).ToListAsync(cancellationToken);

    public async Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
        => await _specificationEvaluator.ApplySpecification(_dbContext.Set<TEntity>(), specification).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> FindAsNoTrackingAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
        => await _specificationEvaluator.ApplySpecification(_dbContext.Set<TEntity>().AsNoTracking(), specification).ToListAsync(cancellationToken);

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().LongCountAsync(cancellationToken);

    public async Task<bool> AnyAsync(TId id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AnyAsync(e => EF.Property<TId>(e, "Id")!.Equals(id!), cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    // ─── Legacy "load everything" helpers ───────────────────────────────────
    // These are intentionally NOT on the IRepository interface. They remain on the
    // concrete class so that callers who really need them (migrations, batch jobs)
    // can opt in, but the type system no longer encourages their use.

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> GetAllAsNoTrackingAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);
}
