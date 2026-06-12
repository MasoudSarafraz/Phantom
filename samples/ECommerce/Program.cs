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

app.MapGet("/", () => """
    <html><body style='font-family:Segoe UI,sans-serif;padding:40px;max-width:800px;margin:auto'>
    <h1>🛒 ECommerce Sample — Phantom Framework</h1>
    <h2>API Endpoints</h2>
    <table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse'>
    <tr style='background:#f0f0f0'><th>Method</th><th>URL</th><th>Body</th></tr>
    <tr><td>POST</td><td>/api/customers</td><td>{"firstName":"Ali","lastName":"Mohammadi","email":"ali@test.com"}</td></tr>
    <tr><td>GET</td><td>/api/customers/{id}</td><td>—</td></tr>
    <tr><td>POST</td><td>/api/products</td><td>{"name":"Laptop","description":"Gaming","price":25000000,"currency":"IRR","stockQuantity":10}</td></tr>
    <tr><td>GET</td><td>/api/products/{id}</td><td>—</td></tr>
    <tr><td>POST</td><td>/api/orders</td><td>{"customerId":"...","shippingAddress":"Tehran","lines":[{"productId":"...","productName":"Laptop","quantity":1,"unitPrice":25000000,"currency":"IRR"}]}</td></tr>
    <tr><td>GET</td><td>/api/orders/{id}</td><td>—</td></tr>
    <tr><td>POST</td><td>/api/orders/{id}/ship</td><td>{"trackingNumber":"TRK-123"}</td></tr>
    <tr><td>POST</td><td>/api/orders/{id}/cancel</td><td>—</td></tr>
    <tr><td>GET</td><td>/health</td><td>—</td></tr>
    </table>
    <p style='color:#666;margin-top:20px'>Use Postman, cURL, or VS Code REST Client to test these endpoints.</p>
    </body></html>
    """);

app.Run();

public partial class Program { }
