using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;
using Phantom.Data.Services;

namespace Phantom.Data.Interceptors;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;

    public SoftDeleteInterceptor(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        ApplySoftDelete(eventData);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void ApplySoftDelete(DbContextEventData eventData)
    {
        var entries = eventData.Context?.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable)
            .ToList();

        if (entries == null) return;

        var currentUser = _currentUserService?.GetCurrentUserId();

        foreach (var entry in entries)
        {
            entry.State = EntityState.Modified;

            var entity = entry.Entity;

            if (entity is AuditableSoftDeleteEntity<Guid> auditableSoftDeleteGuid)
            {
                auditableSoftDeleteGuid.SoftDelete(currentUser);
            }
            else if (entity is AuditableSoftDeleteEntity<int> auditableSoftDeleteInt)
            {
                auditableSoftDeleteInt.SoftDelete(currentUser);
            }
            else if (entity is AuditableSoftDeleteEntity<string> auditableSoftDeleteStr)
            {
                auditableSoftDeleteStr.SoftDelete(currentUser);
            }
            else if (entity is SoftDeleteEntity<Guid> softDeleteGuid)
            {
                softDeleteGuid.SoftDelete();
            }
            else if (entity is SoftDeleteEntity<int> softDeleteInt)
            {
                softDeleteInt.SoftDelete();
            }
            else if (entity is SoftDeleteEntity<string> softDeleteStr)
            {
                softDeleteStr.SoftDelete();
            }
            else
            {
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = DateTimeOffset.UtcNow;

                var deletedByProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedBy");
                if (deletedByProperty != null)
                {
                    deletedByProperty.CurrentValue = currentUser;
                }
            }
        }
    }
}
