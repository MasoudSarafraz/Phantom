using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Phantom.Core.Domain;
using Phantom.Data.Services;

namespace Phantom.Data.Interceptors;

/// <summary>
/// EF Core save changes interceptor that automatically sets audit fields
/// (CreatedAt/CreatedBy, UpdatedAt/UpdatedBy) for entities implementing <see cref="IAuditable"/>.
/// </summary>
public class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditableInterceptor"/> class.
    /// </summary>
    /// <param name="currentUserService">Optional service for resolving the current user for audit fields.</param>
    public AuditableInterceptor(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditable(eventData);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        ApplyAuditable(eventData);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    /// <summary>
    /// Applies audit fields to all <see cref="IAuditable"/> entities being added or modified.
    /// For added entities, sets CreatedAt/CreatedBy; for modified entities, sets UpdatedAt/UpdatedBy.
    /// </summary>
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
