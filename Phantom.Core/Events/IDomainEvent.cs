namespace Phantom.Core.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}