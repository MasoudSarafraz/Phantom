namespace Phantom.Core.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string EventName { get; }
}