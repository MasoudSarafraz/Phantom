using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Exceptions;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Data.EfCore;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.NET.Extensions;
using Phantom.NET.HealthChecks;
using Phantom.NET.Middleware;
using Phantom.NET.ProblemDetails;
using System.Net;
using System.Text.Json;

namespace Phantom.Tests.NET;

public class ExceptionHandlingMiddlewareAdvancedTests
{
    private static TestServer CreateServer(Func<HttpContext, Task> handler, bool continueAfterStarted = false)
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.Run(async ctx => await handler(ctx));
            })
            .ConfigureServices(services => services.AddLogging());

        return new TestServer(builder);
    }

    [Fact]
    public async Task ValidationException_Should_Return_400_With_Errors_Dictionary()
    {
        var failures = new[]
        {
            new ValidationFailure("Name", "Name is required"),
            new ValidationFailure("Age", "Age must be positive")
        };
        var validationException = new ValidationException(failures);

        using var server = CreateServer(ctx => throw validationException);
        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal(400, problem!.Status);
        Assert.Equal("Validation Error", problem.Title);
        Assert.NotNull(problem.Errors);
        Assert.True(problem.Errors!.ContainsKey("Name"));
        Assert.True(problem.Errors!.ContainsKey("Age"));
    }

    [Fact]
    public async Task OperationCanceledException_Should_Return_499()
    {
        using var server = CreateServer(ctx => throw new OperationCanceledException());
        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal(499, problem!.Status);
        Assert.Equal("Request Canceled", problem.Title);
    }

    [Fact]
    public async Task Response_Already_Started_Should_Not_Be_Handled()
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.Run(async ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.StartAsync();
                    await ctx.Response.WriteAsync("Started");
                    throw new InvalidOperationException("Too late");
                });
            })
            .ConfigureServices(services => services.AddLogging());

        using var server = new TestServer(builder);
        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Started", body);
    }

    [Fact]
    public async Task No_Exception_Should_Preserve_Original_Status_Code()
    {
        using var server = CreateServer(ctx =>
        {
            ctx.Response.StatusCode = 204;
            return Task.CompletedTask;
        });
        var client = server.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task BusinessRuleException_Should_Include_Message_As_Detail()
    {
        using var server = CreateServer(ctx =>
            throw new BusinessRuleException("Cannot place order on Sunday"));

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal("Cannot place order on Sunday", problem!.Detail);
    }

    [Fact]
    public async Task NotFoundException_Should_Include_Entity_Info_In_Message()
    {
        using var server = CreateServer(ctx =>
            throw new NotFoundException("User", 42));

        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<PhantomProblemDetail>(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("User", problem!.Detail);
        Assert.Contains("42", problem.Detail);
    }

    [Fact]
    public async Task Content_Type_Should_Always_Be_Problem_Json()
    {
        using var server = CreateServer(ctx => throw new DomainException("err"));
        var client = server.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

public class ApplicationBuilderExtensionsTests
{
    [Fact]
    public void UsePhantom_Should_Return_Same_Builder_Instance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var builder = new ApplicationBuilder(provider);
        var returned = builder.UsePhantom();

        Assert.Same(builder, returned);
    }

    [Fact]
    public async Task StartPhantomChannelsAsync_Should_Start_All_Adapters()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var startedAdapter = new InMemoryChannelAdapter("a1",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        registry.Register("a1", startedAdapter);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var provider = services.BuildServiceProvider();

        var builder = new ApplicationBuilder(provider);
        await builder.StartPhantomChannelsAsync();

        Assert.True(startedAdapter.IsStarted);
    }

    [Fact]
    public async Task StartPhantomChannelsAsync_Should_Continue_On_Adapter_Startup_Failure()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var throwingAdapter = new ThrowingAdapter("bad");
        var okAdapter = new InMemoryChannelAdapter("ok",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        registry.Register("bad", throwingAdapter);
        registry.Register("ok", okAdapter);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var provider = services.BuildServiceProvider();

        var builder = new ApplicationBuilder(provider);
        await builder.StartPhantomChannelsAsync();

        Assert.True(okAdapter.IsStarted);
    }

    [Fact]
    public void StartPhantomChannels_Should_Call_Async_Version_Synchronously()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new InMemoryChannelAdapter("a1",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        registry.Register("a1", adapter);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var provider = services.BuildServiceProvider();

        var builder = new ApplicationBuilder(provider);
        var returned = builder.StartPhantomChannels();

        Assert.Same(builder, returned);
        Assert.True(adapter.IsStarted);
    }

    private sealed class ThrowingAdapter : IChannelAdapter
    {
        public string ChannelName { get; }
        public bool IsStarted { get; private set; }

        public ThrowingAdapter(string name) { ChannelName = name; }

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
            => Task.CompletedTask;

        public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent> { }

        public Task StartAsync(CancellationToken ct = default) => throw new InvalidOperationException("fail");
        public Task StopAsync(CancellationToken ct = default) { IsStarted = false; return Task.CompletedTask; }
    }
}

public class PhantomOptionsFluentApiTests
{
    private static IServiceCollection BuildServices() =>
        new ServiceCollection().AddLogging();

    [Fact]
    public void UsePostgreSQL_With_Empty_ConnectionString_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.UsePostgreSQL(""));
    }

    [Fact]
    public void UseSqlServer_With_Empty_ConnectionString_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.UseSqlServer(""));
    }

    [Fact]
    public void UseInMemoryDatabase_Should_Build_Successfully_Without_Connection_String()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .AddChannel("a", c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { "a" })));

        Assert.Null(ex);
    }

    [Fact]
    public void AddChannel_With_Empty_Name_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.AddChannel("", c => c.UseInMemory()));
    }

    [Fact]
    public void AddChannel_With_Null_Configure_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentNullException>(() => options.AddChannel("x", null!));
    }

    [Fact]
    public void AddChannel_With_ChannelName_Struct_Should_Build()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .AddChannel(ChannelName.From("audit"), c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { "audit" })));

        Assert.Null(ex);
    }

    [Fact]
    public void RouteEvent_With_No_Channels_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.RouteEvent<RouteTestEvent>(Array.Empty<string>()));
    }

    [Fact]
    public void RouteEvent_With_Null_Channels_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.RouteEvent<RouteTestEvent>((string[])null!));
    }

    [Fact]
    public void RouteEvent_With_ChannelNames_Struct_Should_Build()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .AddChannel("a", c => c.UseInMemory())
                      .AddChannel("b", c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { ChannelName.From("a"), ChannelName.From("b") })));

        Assert.Null(ex);
    }

    [Fact]
    public void ConfigureRetry_With_Zero_Retries_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ConfigureRetry(maxRetries: 0));
    }

    [Fact]
    public void ConfigureRetry_Should_Build_Successfully()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .AddChannel("a", c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { "a" })
                      .ConfigureRetry(maxRetries: 5, baseDelay: TimeSpan.FromMilliseconds(250))));

        Assert.Null(ex);
    }

    [Fact]
    public void ConfigureCircuitBreaker_With_Zero_Threshold_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ConfigureCircuitBreaker(failureThreshold: 0));
    }

    [Fact]
    public void ConfigureCircuitBreaker_Should_Build_Successfully()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .AddChannel("a", c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { "a" })
                      .ConfigureCircuitBreaker(failureThreshold: 7, resetTimeout: TimeSpan.FromMinutes(1))));

        Assert.Null(ex);
    }

    [Fact]
    public void UseRabbitMq_With_Empty_Host_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.UseRabbitMq(""));
    }

    [Fact]
    public void UseKafka_With_Empty_Servers_Should_Throw()
    {
        var options = new PhantomOptions();
        Assert.Throws<ArgumentException>(() => options.UseKafka(""));
    }

    [Fact]
    public void Fluent_API_Should_Build_With_Full_Configuration()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .UseFluentValidation()
                      .UseSoftDelete()
                      .UseAuditable()
                      .UseOutbox()
                      .EnableIdempotency()
                      .AddChannel("orders", c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { "orders" })
                      .ConfigureRetry(maxRetries: 3)
                      .ConfigureCircuitBreaker(failureThreshold: 5)));

        Assert.Null(ex);
    }

    [Fact]
    public void DisableOutbox_Should_Build_Successfully()
    {
        var services = BuildServices();
        var ex = Record.Exception(() =>
            services.AddPhantom(typeof(PhantomOptionsFluentApiTests).Assembly,
                o => o.UseInMemoryDatabase()
                      .UseOutbox()
                      .DisableOutbox()
                      .AddChannel("a", c => c.UseInMemory())
                      .RouteEvent<RouteTestEvent>(new[] { "a" })));

        Assert.Null(ex);
    }

    private class RouteTestEvent : IntegrationEvent { }
}

public class PhantomOptionsValidationAdvancedTests
{
    [Fact]
    public void UsePostgreSQL_With_Empty_String_Should_Throw_ArgumentException()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
            services.AddPhantom(typeof(PhantomOptionsValidationAdvancedTests).Assembly, options =>
            {
                options.UsePostgreSQL("");
            }));
    }

    [Fact]
    public void Validate_With_PostgreSQL_And_No_ConnectionString_Should_Throw()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddPhantom(typeof(PhantomOptionsValidationAdvancedTests).Assembly, _ => { }));

        Assert.Contains("connection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_With_RouteEvent_To_Unregistered_Channel_Should_Give_Actionable_Message()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddPhantom(typeof(PhantomOptionsValidationAdvancedTests).Assembly, options =>
            {
                options.UseInMemoryDatabase()
                       .AddChannel("a", c => c.UseInMemory())
                       .RouteEvent<PhantomOptionsValidationTestEvent>(new[] { "non-existent" });
            }));

        Assert.Contains("non-existent", ex.Message);
        Assert.Contains("AddChannel", ex.Message);
    }

    [Fact]
    public void Validate_With_No_Database_Provider_Should_Throw()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddPhantom(typeof(PhantomOptionsValidationAdvancedTests).Assembly, options =>
            {
                options.AddChannel("a", c => c.UseInMemory())
                       .RouteEvent<PhantomOptionsValidationTestEvent>(new[] { "a" });
            }));

        Assert.Contains("connection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

internal class PhantomOptionsValidationTestEvent : IntegrationEvent
{
    public string Payload { get; } = "x";
}

public class DatabaseHealthCheckAdvancedTests
{
    private class HealthCheckDbContext : PhantomDbContext
    {
        public HealthCheckDbContext(DbContextOptions options) : base(options) { }
    }

    [Fact]
    public async Task Unreachable_Database_Should_Return_Unhealthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HealthCheckDbContext>(o =>
        {
            o.UseInMemoryDatabase("HealthCheckUnreachable_" + Guid.NewGuid());
        });
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<HealthCheckDbContext>());

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new DatabaseHealthCheck(scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task HealthCheck_Exception_Should_Return_Unhealthy_With_Exception()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<PhantomDbContext>(_ =>
            throw new InvalidOperationException("DB unavailable"));

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new DatabaseHealthCheck(scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}

public class BrokerHealthCheckAdvancedTests
{
    [Fact]
    public async Task Channel_With_NotStarted_Adapter_Should_Return_Degraded()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter1 = new InMemoryChannelAdapter("ch",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        var adapter2 = new InMemoryChannelAdapter("ch",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        await adapter1.StartAsync();
        registry.Register("ch", adapter1);
        registry.Register("ch", adapter2);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new BrokerHealthCheck("ch", scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Channel_With_All_Adapters_Started_Should_Return_Healthy()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter1 = new InMemoryChannelAdapter("ch",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        var adapter2 = new InMemoryChannelAdapter("ch",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        await adapter1.StartAsync();
        await adapter2.StartAsync();
        registry.Register("ch", adapter1);
        registry.Register("ch", adapter2);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(registry);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new BrokerHealthCheck("ch", scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Missing_Registry_Should_Return_Unhealthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChannelRegistry>(_ =>
            throw new InvalidOperationException("no registry"));

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var check = new BrokerHealthCheck("ch", scopeFactory);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

public class PhantomProblemDetailSerializationTests
{
    [Fact]
    public void Should_Serialize_To_CamelCase_Json()
    {
        var detail = new PhantomProblemDetail
        {
            Status = 422,
            Title = "Business Rule",
            Detail = "Cannot delete active order",
            Type = "https://tools.ietf.org/html/rfc4918",
            Instance = "/api/orders/1",
            TraceId = "trace-abc"
        };

        var json = JsonSerializer.Serialize(detail,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"status\":422", json);
        Assert.Contains("\"title\":\"Business Rule\"", json);
        Assert.Contains("\"detail\":\"Cannot delete active order\"", json);
        Assert.Contains("\"traceId\":\"trace-abc\"", json);
        Assert.Contains("\"instance\":\"/api/orders/1\"", json);
    }

    [Fact]
    public void Should_Allow_Null_Type_And_Instance()
    {
        var detail = new PhantomProblemDetail { Status = 500 };
        Assert.Null(detail.Type);
        Assert.Null(detail.Instance);
        Assert.Null(detail.TraceId);
        Assert.Null(detail.Errors);
    }
}
