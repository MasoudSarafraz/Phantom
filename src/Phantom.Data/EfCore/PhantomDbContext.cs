using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.EfCore;

/// <summary>
/// Base DbContext for Phantom-based applications. Responsibilities:
///   1. Apply EF configurations from this assembly (OutboxMessageConfiguration, ProcessedEventConfiguration, …).
///   2. Apply a global query filter on every <see cref="ISoftDeletable"/> entity so soft-deleted
///      rows are excluded by default.
///   3. Coordinate with <see cref="DomainEventOutboxDispatcher"/> to enqueue domain events into
///      the outbox table in the same SaveChanges transaction, OR dispatch them in-process when
///      the outbox is not in use.
///
/// The actual domain-event pipeline lives in <see cref="DomainEventOutboxDispatcher"/>; this
/// class is a thin DbContext that delegates to it. This separation makes the dispatcher
/// unit-testable and lets downstream apps swap the dispatcher without subclassing the DbContext.
/// </summary>
public class PhantomDbContext : DbContext
{
    private readonly DomainEventOutboxDispatcher? _dispatcher;
    private readonly ILogger<PhantomDbContext>? _logger;

    public PhantomDbContext(
        DbContextOptions options,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ILogger<PhantomDbContext>? logger = null,
        IMessageSerializer? messageSerializer = null)
        : base(options)
    {
        _logger = logger;
        // Construct the dispatcher even when messageSerializer is null — the in-process
        // dispatch path still requires a dispatcher to invoke IDomainEventDispatcher.
        if (domainEventDispatcher is not null || messageSerializer is not null)
        {
            _dispatcher = new DomainEventOutboxDispatcher(
                domainEventDispatcher,
                messageSerializer,
                logger: null);
        }
    }

    /// <summary>
    /// True when domain events should be persisted to the outbox table instead of dispatched
    /// in-process. This is decided by whether an <see cref="IMessageSerializer"/> was injected
    /// at construction time.
    /// </summary>
    public bool UseOutboxForDomainEvents => _dispatcher?.UseOutboxForDomainEvents ?? false;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: collect domain events from aggregate roots and (in outbox mode) enqueue
        // corresponding OutboxMessage rows into the ChangeTracker so they are saved in the
        // same transaction as the aggregate changes.
        var collected = _dispatcher is null
            ? Array.Empty<(IAggregateRoot, IReadOnlyList<IDomainEvent>)>()
            : _dispatcher.CollectAndEnqueueOutbox(this);

        // Step 2: actually persist everything (aggregates + outbox rows).
        var result = await base.SaveChangesAsync(cancellationToken);

        // Step 3: in in-process mode, dispatch events to handlers; in outbox mode, just clear
        // events from the aggregates. Either way, the aggregates end up with no pending events.
        if (_dispatcher is not null && collected.Count > 0)
        {
            await _dispatcher.AfterSaveChangesAsync(collected, cancellationToken);
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhantomDbContext).Assembly);

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
