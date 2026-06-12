using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.AspNetCore.Middleware;
using Phantom.Messaging.Abstractions;

namespace Phantom.AspNetCore.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePhantom(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }

    public static IApplicationBuilder StartPhantomChannels(this IApplicationBuilder app)
    {
        StartPhantomChannelsAsync(app).GetAwaiter().GetResult();
        return app;
    }

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

internal class ChannelStarter { }
