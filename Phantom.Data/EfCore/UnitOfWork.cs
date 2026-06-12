using Microsoft.EntityFrameworkCore.Storage;
using Phantom.Core.Services;

namespace Phantom.Data.EfCore;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// Delegates save operations to <see cref="PhantomDbContext"/> and supports
/// explicit transaction management. Does not dispose the DbContext since
/// the DI container owns its lifetime.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly PhantomDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public UnitOfWork(PhantomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _dbContext.SaveChangesAsync(ct);

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="ct">A token to cancel the asynchronous operation.</param>
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _dbContext.Database.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// Commits the current database transaction.
    /// </summary>
    /// <param name="ct">A token to cancel the asynchronous operation.</param>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Rolls back the current database transaction.
    /// </summary>
    /// <param name="ct">A token to cancel the asynchronous operation.</param>
    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Disposes the unit of work, including any active transaction.
    /// Does NOT dispose the <see cref="PhantomDbContext"/> because the DI container
    /// owns the DbContext lifetime and will dispose it appropriately.
    /// </summary>
    public void Dispose()
    {
        _transaction?.Dispose();
        // Intentionally not disposing _dbContext — DI owns the lifetime
    }

    /// <summary>
    /// Asynchronously disposes the unit of work, including any active transaction.
    /// Does NOT dispose the <see cref="PhantomDbContext"/> because the DI container
    /// owns the DbContext lifetime and will dispose it appropriately.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }
        // Intentionally not disposing _dbContext — DI owns the lifetime
    }
}
