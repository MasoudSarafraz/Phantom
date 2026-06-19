namespace Phantom.Core.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOn { get; }

    string EventName { get; }

    string? CorrelationId { get; }
}

public abstract class IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;

    public virtual string EventName => GetType().Name;

    public string? CorrelationId { get; set; }
}
