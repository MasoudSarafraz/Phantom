using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;
using Phantom.Data.Services;

namespace Phantom.Data.Interceptors;

/// <summary>
/// EF Core save changes interceptor that converts hard deletes into soft deletes
/// for entities implementing <see cref="ISoftDeletable"/>.
/// Supports both <see cref="SoftDeleteEntity{TId}"/> and <see cref="AuditableSoftDeleteEntity{TId}"/>.
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftDeleteInterceptor"/> class.
    /// </summary>
    /// <param name="currentUserService">Optional service for resolving the current user, used for AuditableSoftDeleteEntity.</param>
    public SoftDeleteInterceptor(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        ApplySoftDelete(eventData);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    /// <summary>
    /// Converts hard-deleted <see cref="ISoftDeletable"/> entities into soft deletes
    /// by changing their state to Modified and calling the appropriate SoftDelete method.
    /// </summary>
    private void ApplySoftDelete(DbContextEventData eventData)
    {
        // Materialize the list first to avoid modifying the collection during iteration
        var entries = eventData.Context?.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable)
            .ToList();

        if (entries == null) return;

        var currentUser = _currentUserService?.GetCurrentUserId();

        foreach (var entry in entries)
        {
            entry.State = EntityState.Modified;

            var entity = entry.Entity;

            // Check for known base types first, using their typed SoftDelete methods
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
                // Fallback: for any other ISoftDeletable entity, set properties directly via EF change tracker
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = DateTimeOffset.UtcNow;

                // Try to set DeletedBy if the property exists on the entity
                var deletedByProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedBy");
                if (deletedByProperty != null)
                {
                    deletedByProperty.CurrentValue = currentUser;
                }
            }
        }
    }
}
