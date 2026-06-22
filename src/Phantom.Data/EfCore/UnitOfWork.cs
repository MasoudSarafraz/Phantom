using Microsoft.EntityFrameworkCore.Storage;
using Phantom.Core.Services;

namespace Phantom.Data.EfCore;

public class UnitOfWork : IUnitOfWork
{
    private readonly PhantomDbContext _dbContext;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(PhantomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _dbContext.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already in progress. Call CommitAsync or RollbackAsync before starting a new transaction.");
        }
        _transaction = await _dbContext.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.CommitAsync(ct);
        }
        catch
        {
            await SafeRollbackAsync(ct);
            throw;
        }
        finally
        {
            await SafeDisposeTransactionAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await SafeRollbackAsync(ct);
        await SafeDisposeTransactionAsync();
    }

    private async Task SafeRollbackAsync(CancellationToken ct)
    {
        if (_transaction is null) return;

        try
        {
            await _transaction.RollbackAsync(ct);
        }
        catch
        {
            // Continue disposing the transaction even if rollback failed — the database
            // server will eventually roll back an abandoned transaction, and re-throwing
            // here would mask the original error.
        }
    }

    private async Task SafeDisposeTransactionAsync()
    {
        if (_transaction is null) return;

        try
        {
            await _transaction.DisposeAsync();
        }
        catch
        {
            // Best-effort disposal; transaction server state will be cleaned up on connection close.
        }
        finally
        {
            _transaction = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnitOfWork));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_transaction is not null)
        {
            try
            {
                _transaction.Rollback();
            }
            catch
            {
                // Best-effort rollback on dispose; the database will roll back the
                // transaction when the connection is released.
            }
            try
            {
                _transaction.Dispose();
            }
            catch
            {
                // Best-effort disposal.
            }
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_transaction is not null)
        {
            await SafeRollbackAsync(CancellationToken.None);
            await SafeDisposeTransactionAsync();
        }
    }
}
