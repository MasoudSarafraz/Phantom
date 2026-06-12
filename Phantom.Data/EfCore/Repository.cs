using Microsoft.EntityFrameworkCore;
using Phantom.Core.Services;
using Phantom.Core.Specifications;
using Phantom.Data.Specifications;

namespace Phantom.Data.EfCore;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TId,TEntity}"/>.
/// Provides standard CRUD operations with specification and AsNoTracking support.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class Repository<TId, TEntity> : IRepository<TId, TEntity> where TEntity : class
{
    private readonly PhantomDbContext _dbContext;
    private readonly ISpecificationEvaluator _specificationEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="Repository{TId,TEntity}"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="specificationEvaluator">The specification evaluator for applying specifications to queries.</param>
    public Repository(PhantomDbContext dbContext, ISpecificationEvaluator specificationEvaluator)
    {
        _dbContext = dbContext;
        _specificationEvaluator = specificationEvaluator;
    }

    /// <inheritdoc/>
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().FindAsync(new object[] { id! }, cancellationToken);

    /// <summary>
    /// Gets an entity by its identifier without tracking it in the change tracker.
    /// Useful for read-only scenarios to improve performance.
    /// </summary>
    /// <param name="id">The entity's identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity if found; otherwise, <c>null</c>.</returns>
    public async Task<TEntity?> GetByIdAsNoTrackingAsync(TId id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<TId>(e, "Id")!.Equals(id!), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().ToListAsync(cancellationToken);

    /// <summary>
    /// Gets all entities without tracking them in the change tracker.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of all entities.</returns>
    public async Task<IReadOnlyList<TEntity>> GetAllAsNoTrackingAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
        => await _specificationEvaluator.ApplySpecification(_dbContext.Set<TEntity>(), specification).ToListAsync(cancellationToken);

    /// <summary>
    /// Finds entities matching the specification without tracking them in the change tracker.
    /// </summary>
    /// <param name="specification">The specification to match.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of matching entities.</returns>
    public async Task<IReadOnlyList<TEntity>> FindAsNoTrackingAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
        => await _specificationEvaluator.ApplySpecification(_dbContext.Set<TEntity>().AsNoTracking(), specification).ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TEntity>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().Skip(skip).Take(take).ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().LongCountAsync(cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Uses <see cref="EntityFrameworkQueryableExtensions.AnyAsync{TSource}"/> for efficient
    /// existence checking without loading the entire entity.
    /// </remarks>
    public async Task<bool> AnyAsync(TId id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AnyAsync(e => EF.Property<TId>(e, "Id")!.Equals(id!), cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    /// <inheritdoc/>
    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Remove(entity);
        return Task.CompletedTask;
    }
}
