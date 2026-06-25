using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Phantom.Telemetry.Extensions;

public class PhantomTelemetryOptions
{
    public string ServiceName { get; set; } = "Phantom.App";

    public string ServiceVersion { get; set; } = "1.0.0";

    public string? ServiceNamespace { get; set; }

    public string? OtlpEndpoint { get; set; }

    public bool EnableTracing { get; set; } = true;

    public bool EnableMetrics { get; set; } = true;

    public bool EnableAspNetCoreInstrumentation { get; set; } = true;

    public bool EnableHttpClientInstrumentation { get; set; } = true;

    public bool EnableEntityFrameworkInstrumentation { get; set; } = true;

    public bool EnablePrometheusEndpoint { get; set; } = true;

    public string PrometheusEndpoint { get; set; } = "/metrics";

    public bool EnableConsoleExporter { get; set; }

    public double TraceSampleRatio { get; set; } = 1.0;

    public bool IncludeDomainEvents { get; set; } = true;

    public bool IncludeOutbox { get; set; } = true;

    public bool IncludeMessaging { get; set; } = true;

    public bool IncludeCqrs { get; set; } = true;
}

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddPhantomTelemetry(
        this IServiceCollection services,
        Action<PhantomTelemetryOptions>? configure = null)
    {
        var options = new PhantomTelemetryOptions();
        configure?.Invoke(options);

        var builder = services.AddOpenTelemetry();

        builder.ConfigureResource(r =>
        {
            r.AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceNamespace: options.ServiceNamespace);

            r.AddAttributes(new Dictionary<string, object>
            {
                ["framework"] = "Phantom",
                ["framework.version"] = "1.0.0"
            });
        });

        if (options.EnableTracing)
        {
            builder.WithTracing(tp =>
            {
                tp.AddSource(PhantomTelemetry.ActivitySourceName);

                if (options.EnableAspNetCoreInstrumentation)
                    tp.AddAspNetCoreInstrumentation();

                if (options.EnableHttpClientInstrumentation)
                    tp.AddHttpClientInstrumentation();

                if (options.EnableEntityFrameworkInstrumentation)
                    tp.AddEntityFrameworkCoreInstrumentation();

                tp.SetSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio));

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tp.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }

                if (options.EnableConsoleExporter)
                    tp.AddConsoleExporter();
            });
        }

        if (options.EnableMetrics)
        {
            builder.WithMetrics(mp =>
            {
                mp.AddMeter(PhantomTelemetry.MeterName);

                if (options.EnableAspNetCoreInstrumentation)
                    mp.AddAspNetCoreInstrumentation();

                if (options.EnableHttpClientInstrumentation)
                    mp.AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    mp.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }

                if (options.EnableConsoleExporter)
                    mp.AddConsoleExporter();

                if (options.EnablePrometheusEndpoint)
                {
                    mp.AddPrometheusExporter();
                }
            });
        }

        return services;
    }
}
