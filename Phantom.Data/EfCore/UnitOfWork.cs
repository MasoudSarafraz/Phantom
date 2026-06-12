using Phantom.Core.Services;

namespace Phantom.Data.EfCore;

public class UnitOfWork : IUnitOfWork
{
    private readonly PhantomDbContext _dbContext;
    public UnitOfWork(PhantomDbContext dbContext) { _dbContext = dbContext; }
    public async Task<int> SaveChangesAsync(CancellationToken ct = default) => await _dbContext.SaveChangesAsync(ct);
    public void Dispose() => _dbContext.Dispose();
}
