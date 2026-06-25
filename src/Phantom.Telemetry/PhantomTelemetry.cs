using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Phantom.Telemetry;

public static class PhantomTelemetry
{
    public const string ActivitySourceName = "Phantom.Framework";
    public const string MeterName = "Phantom.Framework";
    public const string Version = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    public static readonly Meter Meter = new(MeterName, Version);

    public static readonly Histogram<double> CommandDurationHistogram = Meter.CreateHistogram<double>(
        name: "phantom.command.duration",
        unit: "ms",
        description: "Duration of command handler execution in milliseconds.");

    public static readonly Histogram<double> QueryDurationHistogram = Meter.CreateHistogram<double>(
        name: "phantom.query.duration",
        unit: "ms",
        description: "Duration of query handler execution in milliseconds.");

    public static readonly Counter<long> PublishCounter = Meter.CreateCounter<long>(
        name: "phantom.publish.total",
        description: "Total number of events published to brokers.");

    public static readonly Counter<long> ConsumeCounter = Meter.CreateCounter<long>(
        name: "phantom.consume.total",
        description: "Total number of events consumed from brokers.");

    public static readonly Counter<long> OutboxPendingCounter = Meter.CreateCounter<long>(
        name: "phantom.outbox.processed.total",
        description: "Total number of outbox messages processed (success or failure).");

    public static readonly Counter<long> HandlerErrorsCounter = Meter.CreateCounter<long>(
        name: "phantom.handler.errors.total",
        description: "Total number of handler execution failures.");

    public static readonly Histogram<double> OutboxProcessingDurationHistogram = Meter.CreateHistogram<double>(
        name: "phantom.outbox.processing.duration",
        unit: "ms",
        description: "Duration of outbox message processing in milliseconds.");
}

public static class PhantomTelemetryTags
{
    public const string CommandType = "phantom.command.type";
    public const string QueryType = "phantom.query.type";
    public const string EventType = "phantom.event.type";
    public const string ChannelName = "phantom.channel.name";
    public const string AdapterType = "phantom.adapter.type";
    public const string Status = "phantom.status";
    public const string Error = "phantom.error";
    public const string MessageId = "phantom.message.id";
    public const string Operation = "phantom.operation";

    public const string StatusSuccess = "success";
    public const string StatusFailure = "failure";
}
