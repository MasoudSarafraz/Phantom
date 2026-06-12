using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;

namespace Phantom.Data.Interceptors;

public interface ICurrentUserService { string? GetCurrentUserId(); }

public class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;
    public AuditableInterceptor(ICurrentUserService? currentUserService = null) { _currentUserService = currentUserService; }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) { ApplyAuditable(eventData); return base.SavingChanges(eventData, result); }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default) { ApplyAuditable(eventData); return base.SavingChangesAsync(eventData, result, ct); }

    private void ApplyAuditable(DbContextEventData eventData)
    {
        var currentUser = _currentUserService?.GetCurrentUserId();
        var entries = eventData.Context?.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry.Entity is AuditableEntity<Guid> auditableGuid) { if (entry.State == EntityState.Added) auditableGuid.SetCreated(currentUser); else auditableGuid.SetUpdated(currentUser); }
            else if (entry.Entity is AuditableEntity<int> auditableInt) { if (entry.State == EntityState.Added) auditableInt.SetCreated(currentUser); else auditableInt.SetUpdated(currentUser); }
            else if (entry.Entity is AuditableEntity<string> auditableStr) { if (entry.State == EntityState.Added) auditableStr.SetCreated(currentUser); else auditableStr.SetUpdated(currentUser); }
        }
    }
}
