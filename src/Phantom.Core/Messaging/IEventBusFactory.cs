namespace Phantom.Core.Messaging;
public interface IEventBusFactory
{
    IEventBus GetEventBus(string busName);
}
