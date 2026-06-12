using Phantom.AspNetCore.Middleware;
using Phantom.AspNetCore.ProblemDetails;
using Phantom.AspNetCore.HealthChecks;
using Phantom.AspNetCore.Extensions;
using Phantom.Core.Exceptions;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Data.EfCore;
using Phantom.Data.Services;
using Phantom.Messaging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Net;
using System.Text.Json;

namespace Phantom.Tests.AspNetCore;

// ─── ExceptionHandlingMiddleware Tests ───────────────────────────

public class ExceptionHandlingMiddlewareTests
{
    private TestServer CreateServer(Func<HttpContext, Task> pipelineHandler)
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.Run(async ctx => await pipelineHandler(ctx));
            })
            .ConfigureServices(services =>
            {
                services.AddLogging();
            });

        return new TestServer(builder);
    }

    [Fact]
    public async Task NotFoundException_Should_Return_404()
    {
        using var server = CreateServer(ctx =>
        {
            throw new NotFoundException("User", 42);
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Not Found", problem.Title);
    }

    [Fact]
    public async Task BusinessRuleException_Should_Return_422()
    {
        using var server = CreateServer(ctx =>
        {
            throw new BusinessRuleException("Cannot delete active order");
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(422, problem!.Status);
        Assert.Equal("Business Rule Violation", problem.Title);
    }

    [Fact]
    public async Task ConcurrencyException_Should_Return_409()
    {
        using var server = CreateServer(ctx =>
        {
            throw new ConcurrencyException("Order", 1);
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(409, problem!.Status);
        Assert.Equal("Concurrency Conflict", problem.Title);
    }

    [Fact]
    public async Task DomainException_Should_Return_422()
    {
        using var server = CreateServer(ctx =>
        {
            throw new DomainException("Domain error occurred");
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(422, problem!.Status);
        Assert.Equal("Domain Error", problem.Title);
    }

    [Fact]
    public async Task Unknown_Exception_Should_Return_500()
    {
        using var server = CreateServer(ctx =>
        {
            throw new InvalidOperationException("Unexpected error");
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(500, problem!.Status);
        Assert.Equal("Internal Server Error", problem.Title);
    }

    [Fact]
    public async Task Response_Should_Include_TraceId()
    {
        using var server = CreateServer(ctx =>
        {
            throw new NotFoundException("User", 1);
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(problem!.TraceId);
        Assert.NotEmpty(problem.TraceId);
    }

    [Fact]
    public async Task Response_Should_Include_Instance()
    {
        using var server = CreateServer(ctx =>
        {
            throw new NotFoundException("User", 1);
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/test/path");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal("/test/path", problem!.Instance);
    }

    [Fact]
    public async Task BusinessRuleException_With_Code_Should_Return_422()
    {
        using var server = CreateServer(ctx =>
        {
            throw new BusinessRuleException("RULE001", "Cannot delete active order");
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task No_Exception_Should_Pass_Through()
    {
        using var server = CreateServer(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return ctx.Response.WriteAsync("OK");
        });

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// ─── PhantomProblemDetail Tests ─────────────────────────────────

public class PhantomProblemDetailTests
{
    [Fact]
    public void Should_Have_RFC7807_Properties()
    {
        var detail = new PhantomProblemDetail
        {
            Status = 400,
            Title = "Validation Error",
            Detail = "Name is required",
            Type = "https://httpstatuses.com/400",
            Instance = "/api/orders",
            TraceId = "trace-123"
        };

        Assert.Equal(400, detail.Status);
        Assert.Equal("Validation Error", detail.Title);
        Assert.Equal("Name is required", detail.Detail);
        Assert.Equal("https://httpstatuses.com/400", detail.Type);
        Assert.Equal("/api/orders", detail.Instance);
        Assert.Equal("trace-123", detail.TraceId);
    }

    [Fact]
    public void Errors_Should_Be_Dictionary()
    {
        var detail = new PhantomProblemDetail
        {
            Status = 400,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } },
                { "Age", new[] { "Age must be positive", "Age is required" } }
            }
        };

        Assert.NotNull(detail.Errors);
        Assert.Equal(2, detail.Errors!.Count);
        Assert.Single(detail.Errors["Name"]);
        Assert.Equal(2, detail.Errors["Age"].Length);
    }
}

// ─── DatabaseHealthCheck Tests ──────────────────────────────────

public class AspNetTestDbContext : PhantomDbContext
{
    public AspNetTestDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher) { }
}

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task Healthy_Database_Should_Return_Healthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AspNetTestDbContext>(o => o.UseInMemoryDatabase("HealthCheck_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<AspNetTestDbContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        var sp = services.BuildServiceProvider();

        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var check = new DatabaseHealthCheck(scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}

// ─── BrokerHealthCheck Tests ────────────────────────────────────

public class BrokerHealthCheckTests
{
    [Fact]
    public async Task No_Channels_Should_Return_Unhealthy()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new BrokerHealthCheck("nonexistent", scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Started_Channel_Should_Return_Healthy()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new TestHealthChannelAdapter("test-ch");
        adapter.StartAsync().Wait();
        registry.Register("test-ch", adapter);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new BrokerHealthCheck("test-ch", scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task NotStarted_Channel_Should_Return_Degraded()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new TestHealthChannelAdapter("test-ch");
        // Not started!
        registry.Register("test-ch", adapter);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new BrokerHealthCheck("test-ch", scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}

// ─── Helper ─────────────────────────────────────────────────────

internal class TestHealthChannelAdapter : IChannelAdapter
{
    public string ChannelName { get; }
    public bool IsStarted => _isStarted;
    private bool _isStarted;

    public TestHealthChannelAdapter(string name) { ChannelName = name; }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
        => Task.CompletedTask;

    public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent> { }

    public Task StartAsync(CancellationToken ct = default) { _isStarted = true; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct = default) { _isStarted = false; return Task.CompletedTask; }
}


