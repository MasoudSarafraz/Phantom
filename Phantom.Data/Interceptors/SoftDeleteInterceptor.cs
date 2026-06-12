using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;

namespace Phantom.Data.Interceptors;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ApplySoftDelete(eventData);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void ApplySoftDelete(DbContextEventData eventData)
    {
        var entries = eventData.Context?.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted);
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry.Entity is SoftDeleteEntity<Guid> softDeleteGuid) { entry.State = EntityState.Modified; softDeleteGuid.SoftDelete(); }
            else if (entry.Entity is SoftDeleteEntity<int> softDeleteInt) { entry.State = EntityState.Modified; softDeleteInt.SoftDelete(); }
            else if (entry.Entity is SoftDeleteEntity<string> softDeleteString) { entry.State = EntityState.Modified; softDeleteString.SoftDelete(); }
        }
    }
}
