using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.EfCore;

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

        if (domainEventDispatcher is not null || messageSerializer is not null)
        {
            _dispatcher = new DomainEventOutboxDispatcher(
                domainEventDispatcher,
                messageSerializer,
                logger: null);
        }
    }

    public bool UseOutboxForDomainEvents => _dispatcher?.UseOutboxForDomainEvents ?? false;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {

        var collected = _dispatcher is null
            ? Array.Empty<(IAggregateRoot, IReadOnlyList<IDomainEvent>)>()
            : _dispatcher.CollectAndEnqueueOutbox(this);

        var result = await base.SaveChangesAsync(cancellationToken);

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
