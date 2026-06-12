using Microsoft.EntityFrameworkCore;
using Phantom.Core.Services;
using Phantom.Core.Specifications;
using Phantom.Data.Specifications;

namespace Phantom.Data.EfCore;

public class Repository<TId, TEntity> : IRepository<TId, TEntity> where TEntity : class
{
    private readonly PhantomDbContext _dbContext;
    private readonly ISpecificationEvaluator _specificationEvaluator;

    public Repository(PhantomDbContext dbContext, ISpecificationEvaluator specificationEvaluator) { _dbContext = dbContext; _specificationEvaluator = specificationEvaluator; }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) => await _dbContext.Set<TEntity>().FindAsync(new object[] { id! }, ct);
    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) => await _dbContext.Set<TEntity>().ToListAsync(ct);
    public async Task<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> spec, CancellationToken ct = default) => await _specificationEvaluator.ApplySpecification(_dbContext.Set<TEntity>(), spec).ToListAsync(ct);
    public async Task<bool> ExistsAsync(TId id, CancellationToken ct = default) => await GetByIdAsync(id, ct) != null;
    public async Task AddAsync(TEntity entity, CancellationToken ct = default) => await _dbContext.Set<TEntity>().AddAsync(entity, ct);
    public void Update(TEntity entity) => _dbContext.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity) => _dbContext.Set<TEntity>().Remove(entity);
}
