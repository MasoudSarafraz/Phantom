using MyApp.Application.Commands;
using MyApp.Application.Handlers;
using MyApp.Domain.Entities;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence;
using Phantom.Core.Services;
using Phantom.Data.EfCore;
using Phantom.NET.Extensions;
using Phantom.Telemetry.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddPhantom(
    new[]
    {
        typeof(Program).Assembly,
        typeof(CreateProductCommand).Assembly,
        typeof(MyAppDbContext).Assembly,
        typeof(ProductCreatedIntegrationEvent).Assembly,
        typeof(Product).Assembly
    },
    builder.Configuration,
    options =>
    {
        options
            .UseFluentValidation()
            .UseSoftDelete()
            .UseAuditable()
            .EnableIdempotency();

        options.AddChannel("default", channel => channel.UseInMemory());
        options.RouteEvent<ProductCreatedIntegrationEvent>("default");
    });

builder.Services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<MyAppDbContext>());
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddPhantomTelemetry(o =>
{
    o.ServiceName = "MyApp.Api";
    o.ServiceVersion = "1.0.0";
    o.OtlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
});

builder.Services.AddPhantomDiagnostics();

builder.Services.AddHealthChecks()
    .AddPhantomDatabaseHealthCheck();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UsePhantom();
app.UsePhantomPrometheusScrapeEndpoint();
app.UsePhantomDiagnostics();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program { }

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUserService(IHttpContextAccessor accessor) { _accessor = accessor; }
    public string? GetCurrentUserId() =>
        _accessor.HttpContext?.User?.Identity?.Name ?? "system";
}
