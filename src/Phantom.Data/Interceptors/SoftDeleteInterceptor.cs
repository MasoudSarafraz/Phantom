using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;
using Phantom.Core.Services;

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

            var entity = (ISoftDeletable)entry.Entity;

            // Use the structured SoftDelete() method instead of setting properties directly
            // This ensures any domain logic in the SoftDelete() override is respected.
            if (entity is AuditableSoftDeleteEntity<Guid> auditableSoftDelete)
            {
                auditableSoftDelete.SoftDelete(currentUser);
            }
            else
            {
                entity.SoftDelete();

                // If the entity has a DeletedBy property (auditable), set it via EF change tracker
                var deletedByProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedBy");
                if (deletedByProperty != null)
                {
                    deletedByProperty.CurrentValue = currentUser;
                }
            }
        }
    }
}
