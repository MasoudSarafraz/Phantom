using ECommerce.Application.IntegrationEvents;
using ECommerce.Domain.Events;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phantom.AspNetCore.Extensions;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.CQRS.Extensions;
using Phantom.Data.EfCore;
using Phantom.Data.Extensions;
using Phantom.Data.Services;
using Phantom.Data.Specifications;
using Phantom.Messaging.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddPhantomCQRS(typeof(Program).Assembly);
builder.Services.AddPhantomValidation();

builder.Services.AddSingleton<ISpecificationEvaluator, EfSpecificationEvaluator>();

builder.Services.AddDbContext<ECommerceDbContext>((sp, options) =>
{
    options.UseInMemoryDatabase("ECommerceDb");
    options.AddInterceptors(
        new Phantom.Data.Interceptors.SoftDeleteInterceptor(sp.GetService<ICurrentUserService>()),
        new Phantom.Data.Interceptors.AuditableInterceptor(sp.GetService<ICurrentUserService>()));
});

builder.Services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
builder.Services.TryAddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<IDomainEventHandler<OrderPlacedEvent>, ECommerce.Application.Handlers.OrderPlacedEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<OrderShippedEvent>, ECommerce.Application.Handlers.OrderShippedEventHandler>();

builder.Services.AddPhantomMessaging(new[] { typeof(Program).Assembly }, messaging =>
{
    messaging.AddChannel("orders", channel => channel.UseInMemory());
    messaging.AddChannel("notifications", channel => channel.UseInMemory());
    messaging.RouteEvent<OrderCreatedIntegrationEvent>("orders", "notifications");
    messaging.RouteEvent<OrderShippedIntegrationEvent>("orders");
});

builder.Services.AddHealthChecks()
    .AddPhantomDatabaseHealthCheck()
    .AddPhantomBrokerHealthCheck("orders");

var app = builder.Build();

app.UsePhantom();
app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Redirect("/health"));

app.Run();

public partial class Program { }
