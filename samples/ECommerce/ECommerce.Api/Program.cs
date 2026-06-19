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

builder.Services.AddPhantom(new[]
{
    typeof(Program).Assembly,
    typeof(ECommerce.Application.Commands.CreateOrderCommand).Assembly,
    typeof(ECommerceDbContext).Assembly,
    typeof(OrderCreatedIntegrationEvent).Assembly,
    typeof(Order).Assembly,
}, options =>
{
    options

        .UseInMemoryDatabase(d =>
        {
            d.UseSoftDelete = true;
            d.UseAuditable = true;
        })

        .UseFluentValidation()

        .EnableIdempotency()

        .AddChannel("orders", channel => channel.UseInMemory())
        .AddChannel("notifications", channel => channel.UseInMemory())

        .RouteEvent<OrderCreatedIntegrationEvent>("orders", "notifications")
        .RouteEvent<OrderShippedIntegrationEvent>("orders")

        .ConfigureRetry(maxRetries: 3, baseDelay: TimeSpan.FromSeconds(1))
        .ConfigureCircuitBreaker(failureThreshold: 5, resetTimeout: TimeSpan.FromSeconds(30));
});

builder.Services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddHealthChecks()
    .AddPhantomDatabaseHealthCheck()
    .AddPhantomBrokerHealthCheck("orders");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UsePhantom();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
