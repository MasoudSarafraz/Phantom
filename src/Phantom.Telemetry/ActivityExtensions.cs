using System.Diagnostics;
using OpenTelemetry.Trace;
using Phantom.Telemetry;

namespace Phantom.Telemetry.ActivityExtensions;

public static class ActivityScopeExtensions
{
    public static Activity? StartPhantomActivity(
        this ActivitySource source,
        string name,
        string operation,
        string? entityType = null,
        string? entityId = null)
    {
        var activity = source.StartActivity(name, ActivityKind.Internal);
        if (activity is null) return null;

        activity.SetTag(PhantomTelemetryTags.Operation, operation);
        if (entityType is not null) activity.SetTag("phantom.entity.type", entityType);
        if (entityId is not null) activity.SetTag("phantom.entity.id", entityId);

        return activity;
    }

    public static void SetSuccess(this Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag(PhantomTelemetryTags.Status, PhantomTelemetryTags.StatusSuccess);
    }

    public static void SetFailure(this Activity? activity, Exception? exception = null, string? error = null)
    {
        activity?.SetStatus(ActivityStatusCode.Error, error ?? exception?.Message);
        activity?.SetTag(PhantomTelemetryTags.Status, PhantomTelemetryTags.StatusFailure);
        if (error is not null) activity?.SetTag(PhantomTelemetryTags.Error, error);
        if (exception is not null) activity?.SetTag("exception.message", exception.Message);
        if (exception is not null) activity?.SetTag("exception.stacktrace", exception.StackTrace);
        if (exception is not null) activity?.SetTag("exception.type", exception.GetType().FullName);
    }
}

public readonly struct ActivityScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly Stopwatch _stopwatch;
    private readonly Action<double>? _onDispose;

    public ActivityScope(Activity? activity, Action<double>? onDispose = null)
    {
        _activity = activity;
        _stopwatch = Stopwatch.StartNew();
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _onDispose?.Invoke(_stopwatch.Elapsed.TotalMilliseconds);
        _activity?.Dispose();
    }
}
