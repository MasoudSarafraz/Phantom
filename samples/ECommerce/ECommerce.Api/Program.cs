using ECommerce.Application.Commands;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Persistence;
using ECommerce.IntegrationContracts;
using Microsoft.EntityFrameworkCore;
using Phantom.NET.Extensions;
using Phantom.Core.Services;
using Phantom.Data.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// ─── Phantom Framework Setup ─────────────────────────────────
//
// AddPhantom() is the single entry point for the entire framework.
// It registers: CQRS + Data + Messaging + all cross-cutting concerns.
//
// In a multi-project Clean Architecture setup we pass several assemblies so Phantom
// can discover handlers in each layer:
//   - ECommerce.Api                → API controllers and minimal host
//   - ECommerce.Application        → Command/Query/Domain-event/Integration-event handlers
//   - ECommerce.Infrastructure     → DbContext, interceptors
//   - ECommerce.IntegrationContracts → Integration event types (so the OutboxProcessor can resolve them)
//   - ECommerce.Domain             → Aggregate roots (so PhantomDbContext can collect domain events)
//
// Features demonstrated in this sample:
//
// 1. UseInMemoryDatabase()     → EF Core InMemory provider (swap with UsePostgreSQL/UseSqlServer for production)
// 2. UseSoftDelete()           → Soft-delete interceptor (sets IsDeleted=true instead of DELETE)
// 3. UseAuditable()            → Auto-fills CreatedAt/UpdatedAt/CreatedBy/UpdatedBy
// 4. UseFluentValidation()     → Validation pipeline behavior (validates commands before handler)
// 5. EnableIdempotency()       → Idempotent integration event handlers (prevents duplicate processing)
// 6. AddChannel()              → Messaging channels (InMemory for dev, RabbitMQ for production)
// 7. RouteEvent()              → Routes integration events to specific channels
// 8. UseOutbox()               → Outbox pattern (ON by default — domain events are never lost)
// 9. ConfigureRetry()          → Retry policy for message publishing (exponential backoff)
// 10. ConfigureCircuitBreaker()→ Circuit breaker for RabbitMQ resilience
//
builder.Services.AddPhantom(new[]
{
    typeof(Program).Assembly,                                            // ECommerce.Api
    typeof(ECommerce.Application.Commands.CreateOrderCommand).Assembly,  // ECommerce.Application
    typeof(ECommerceDbContext).Assembly,                                 // ECommerce.Infrastructure
    typeof(OrderCreatedIntegrationEvent).Assembly,                       // ECommerce.IntegrationContracts
    typeof(Order).Assembly,                                              // ECommerce.Domain
}, options =>
{
    options
        // ─── Data Layer ─────────────────────────────────
        .UseInMemoryDatabase(d =>
        {
            d.UseSoftDelete = true;
            d.UseAuditable = true;
        })
        // For production, use one of:
        // .UsePostgreSQL("Host=localhost;Database=ecommerce;Username=postgres;Password=secret", d =>
        // {
        //     d.UseSoftDelete = true;
        //     d.UseAuditable = true;
        //     d.MigrationsAssembly = typeof(ECommerceDbContext).Assembly.FullName;
        // })
        // .UseSqlServer("Server=localhost;Database=ECommerce;Trusted_Connection=true;", d => { ... })

        // ─── Validation ─────────────────────────────────
        .UseFluentValidation()

        // ─── Idempotency ────────────────────────────────
        // Wraps IIntegrationEventHandler with IIdempotencyTracker check.
        // Duplicate events (same EventId) are automatically skipped.
        .EnableIdempotency()

        // ─── Outbox ─────────────────────────────────────
        // Enabled by default — domain events are saved to OutboxMessage table
        // in the same transaction as aggregate changes, then published asynchronously.
        // Call .DisableOutbox() ONLY for simple CRUD scenarios without events.
        // .UseOutbox()  // ← already default, but you can call explicitly for clarity

        // ─── Messaging Channels ─────────────────────────
        .AddChannel("orders", channel => channel.UseInMemory())
        .AddChannel("notifications", channel => channel.UseInMemory())

        // For production RabbitMQ:
        // .UseRabbitMq("localhost", r =>
        // {
        //     r.Username = "guest";
        //     r.Password = "guest";
        //     r.VirtualHost = "/";
        // })

        // ─── Event Routing ──────────────────────────────
        // Routes integration events to channels.
        // OrderCreatedIntegrationEvent → published to both "orders" and "notifications"
        .RouteEvent<OrderCreatedIntegrationEvent>("orders", "notifications")
        .RouteEvent<OrderShippedIntegrationEvent>("orders")

        // ─── Resilience ─────────────────────────────────
        .ConfigureRetry(maxRetries: 3, baseDelay: TimeSpan.FromSeconds(1))
        .ConfigureCircuitBreaker(failureThreshold: 5, resetTimeout: TimeSpan.FromSeconds(30));
});

// ─── Application-specific registrations ──────────────────────

builder.Services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Note: Domain event handlers are auto-registered by AddPhantom().
// No manual registration needed — the framework scans the assembly
// for all IDomainEventHandler<> implementations.

// ─── Health Checks ───────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddPhantomDatabaseHealthCheck()
    .AddPhantomBrokerHealthCheck("orders");

// ─── App Pipeline ────────────────────────────────────────────

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// UsePhantom() adds:
//   - ExceptionHandlingMiddleware (converts domain exceptions to RFC 7807 Problem Details)
app.UsePhantom();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
