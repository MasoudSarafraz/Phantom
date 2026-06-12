namespace Phantom.Messaging.Outbox;

public interface IOutboxProcessor
{
    Task ProcessAsync(CancellationToken ct = default);
}
