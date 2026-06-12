using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace Phantom.Data.EfCore;

/// <summary>
/// EF Core DbContext for the Phantom framework.
/// Automatically collects and dispatches domain events from aggregate roots
/// implementing <see cref="IAggregateRoot"/>, and applies global query filters
/// for <see cref="ISoftDeletable"/> entities.
/// </summary>
public class PhantomDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;
    private readonly ILogger<PhantomDbContext>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhantomDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    /// <param name="domainEventDispatcher">Optional domain event dispatcher for publishing domain events after save.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public PhantomDbContext(
        DbContextOptions options,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ILogger<PhantomDbContext>? logger = null)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Saves all changes to the database and dispatches domain events from aggregate roots.
    /// Domain events are collected before save, persisted with the transaction,
    /// and dispatched after a successful save. If a handler fails, the remaining
    /// events continue to be dispatched and errors are logged.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events from all IAggregateRoot entities (any TId type)
        var aggregateRoots = ChangeTracker.Entries()
            .Where(e => e.Entity is IAggregateRoot)
            .Select(e => (IAggregateRoot)e.Entity)
            .ToList();

        var domainEvents = aggregateRoots
            .SelectMany(ar => ar.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events after successful save, with per-event error handling
        if (_domainEventDispatcher != null)
        {
            foreach (var domainEvent in domainEvents)
            {
                try
                {
                    await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "[Phantom] Failed to dispatch domain event {EventType}. Continuing with remaining events.",
                        domainEvent.GetType().Name);
                }
            }
        }

        // Clear domain events only after all events have been dispatched (or attempted)
        foreach (var aggregateRoot in aggregateRoots)
        {
            aggregateRoot.ClearDomainEvents();
        }

        return result;
    }

    /// <summary>
    /// Configures the model, including global query filters for soft-deletable entities
    /// and applying all IEntityTypeConfiguration implementations from this assembly.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity type configurations from this assembly (e.g., OutboxMessageConfiguration)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhantomDbContext).Assembly);

        // Apply global query filter for ISoftDeletable entities: exclude soft-deleted records by default
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
