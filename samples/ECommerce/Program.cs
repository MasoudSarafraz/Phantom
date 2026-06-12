using ECommerce.Application.IntegrationEvents;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Phantom.AspNetCore.Extensions;
using Phantom.Data.Interceptors;
using Phantom.Data.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddPhantom(typeof(Program).Assembly, phantom =>
{
    phantom.UseInMemoryDatabase(opt =>
    {
        opt.UseSoftDelete = true;
        opt.UseAuditable = true;
    });

    phantom.AddChannel("orders", channel => channel.UseInMemory());
    phantom.AddChannel("notifications", channel => channel.UseInMemory());

    phantom.RouteEvent<OrderCreatedIntegrationEvent>("orders", "notifications");
    phantom.RouteEvent<OrderShippedIntegrationEvent>("orders");

    phantom.UseFluentValidation();
    phantom.UseSoftDelete();
    phantom.UseAuditable();
    phantom.ConfigureRetry(maxRetries: 3);
    phantom.ConfigureCircuitBreaker(failureThreshold: 5);
});

builder.Services.RemoveAll<PhantomDbContext>();
builder.Services.RemoveAll<DbContextOptions<PhantomDbContext>>();
builder.Services.AddDbContext<ECommerceDbContext>(options =>
{
    options.UseInMemoryDatabase("ECommerceDb");
    options.AddInterceptors(new SoftDeleteInterceptor(), new AuditableInterceptor());
});
builder.Services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());
builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUserService, CurrentUserService>());

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
