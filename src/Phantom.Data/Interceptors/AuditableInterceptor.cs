using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;
using Phantom.Core.Services;

namespace Phantom.Data.Interceptors;

public class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;

    public AuditableInterceptor(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditable(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        ApplyAuditable(eventData);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void ApplyAuditable(DbContextEventData eventData)
    {
        var currentUser = _currentUserService?.GetCurrentUserId();

        var entries = eventData.Context?.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable && (e.State == EntityState.Added || e.State == EntityState.Modified))
            .ToList();

        if (entries == null) return;

        foreach (var entry in entries)
        {
            var auditable = (IAuditable)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                auditable.SetCreated(currentUser);
            }
            else if (entry.State == EntityState.Modified)
            {
                auditable.SetUpdated(currentUser);
            }
        }
    }
}
