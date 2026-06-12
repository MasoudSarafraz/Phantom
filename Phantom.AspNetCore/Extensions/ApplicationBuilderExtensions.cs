using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.AspNetCore.Middleware;
using Phantom.Messaging.Abstractions;

namespace Phantom.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring the Phantom framework middleware and startup
/// on the <see cref="IApplicationBuilder"/> pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Phantom exception handling middleware to the application pipeline.
    /// This middleware catches unhandled exceptions and converts them into
    /// RFC 7807 Problem Detail responses.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> for fluent chaining.</returns>
    public static IApplicationBuilder UsePhantom(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }

    /// <summary>
    /// Starts all registered channel adapters by calling <see cref="IChannelAdapter.StartAsync"/>
    /// on each one. This should be called during application startup to initialize
    /// message consumers and publishers.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> for fluent chaining.</returns>
    public static IApplicationBuilder StartPhantomChannels(this IApplicationBuilder app)
    {
        StartPhantomChannelsAsync(app).GetAwaiter().GetResult();
        return app;
    }

    /// <summary>
    /// Asynchronously starts all registered channel adapters by calling
    /// <see cref="IChannelAdapter.StartAsync"/> on each one. Prefer this over
    /// <see cref="StartPhantomChannels"/> when using in async application startup.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>A task representing the asynchronous startup operation.</returns>
    public static async Task StartPhantomChannelsAsync(this IApplicationBuilder app)
    {
        var registry = app.ApplicationServices.GetRequiredService<IChannelRegistry>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ChannelStarter>>();

        var adapters = registry.GetAllAdapters();

        foreach (var adapter in adapters)
        {
            try
            {
                await adapter.StartAsync();
                logger.LogInformation("[Phantom] Started channel adapter '{Channel}'", adapter.ChannelName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Phantom] Failed to start channel adapter '{Channel}'", adapter.ChannelName);
            }
        }
    }
}

/// <summary>
/// Marker class used for logging channel startup operations.
/// </summary>
internal class ChannelStarter { }
