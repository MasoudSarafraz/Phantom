using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Messaging.Abstractions;
using System.Text.Json;

namespace Phantom.NET.Diagnostics;

public static class DiagnosticsEndpointExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static IApplicationBuilder UsePhantomDiagnostics(
        this IApplicationBuilder app,
        Action<PhantomDiagnosticsOptions>? configure = null)
    {
        var options = new PhantomDiagnosticsOptions();
        configure?.Invoke(options);

        if (!options.Enabled) return app;

        app.Map(options.EndpointPrefix, branch =>
        {
            branch.Run(async context =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<DiagnosticsEndpointsLogger>>();

                try
                {
                    var path = context.Request.Path.Value ?? string.Empty;
                    var response = await RouteDiagnosticsRequestAsync(context, path, options);

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonOptions);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while serving Phantom diagnostics endpoint: {Path}", context.Request.Path);

                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var error = new { error = "Internal error while gathering diagnostics.", detail = ex.Message };
                    await JsonSerializer.SerializeAsync(context.Response.Body, error, JsonOptions);
                }
            });
        });

        return app;
    }

    private static async Task<object> RouteDiagnosticsRequestAsync(
        HttpContext context,
        string path,
        PhantomDiagnosticsOptions options)
    {
        if (path.EndsWith("/channels", StringComparison.OrdinalIgnoreCase))
        {
            if (!options.ExposeChannels)
                return new { error = "Channels diagnostics are disabled." };

            var channelService = context.RequestServices.GetRequiredService<ChannelDiagnosticsService>();
            return channelService.GetChannelsSnapshot();
        }

        if (path.EndsWith("/outbox", StringComparison.OrdinalIgnoreCase))
        {
            if (!options.ExposeOutbox)
                return new { error = "Outbox diagnostics are disabled." };

            var outboxService = context.RequestServices.GetRequiredService<OutboxDiagnosticsService>();
            return await outboxService.GetOutboxSnapshotAsync(context.RequestAborted);
        }

        if (path.EndsWith("/handlers", StringComparison.OrdinalIgnoreCase))
        {
            if (!options.ExposeHandlers)
                return new { error = "Handler diagnostics are disabled." };

            var handlerService = context.RequestServices.GetRequiredService<HandlerDiagnosticsService>();
            return handlerService.GetHandlersSnapshot();
        }

        if (path.EndsWith("/idempotency", StringComparison.OrdinalIgnoreCase))
        {
            if (!options.ExposeIdempotency)
                return new { error = "Idempotency diagnostics are disabled." };

            var idempotencyService = context.RequestServices.GetRequiredService<IdempotencyDiagnosticsService>();
            return await idempotencyService.GetIdempotencySnapshotAsync(context.RequestAborted);
        }

        if (path.EndsWith("/configuration", StringComparison.OrdinalIgnoreCase))
        {
            if (!options.ExposeConfiguration)
                return new { error = "Configuration diagnostics are disabled." };

            var configService = context.RequestServices.GetRequiredService<ConfigurationDiagnosticsService>();
            return configService.GetConfigurationSnapshot();
        }

        return new
        {
            service = "Phantom",
            version = "1.0.0",
            endpoints = new[]
            {
                $"{options.EndpointPrefix}/channels",
                $"{options.EndpointPrefix}/outbox",
                $"{options.EndpointPrefix}/handlers",
                $"{options.EndpointPrefix}/idempotency",
                $"{options.EndpointPrefix}/configuration"
            }
        };
    }

    public static IServiceCollection AddPhantomDiagnostics(this IServiceCollection services, Action<PhantomDiagnosticsOptions>? configure = null)
    {
        var options = new PhantomDiagnosticsOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ChannelDiagnosticsService>();
        services.AddSingleton<OutboxDiagnosticsService>();
        services.AddSingleton<IdempotencyDiagnosticsService>();
        services.AddSingleton<HandlerDiagnosticsService>();
        services.AddSingleton<ConfigurationDiagnosticsService>();

        return services;
    }
}

internal class DiagnosticsEndpointsLogger { }
