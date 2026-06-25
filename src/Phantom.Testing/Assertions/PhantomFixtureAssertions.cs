using Phantom.Core.Events;
using Phantom.Core.Messaging;
using System.Collections.Concurrent;

namespace Phantom.Testing.Assertions;

public static class PhantomFixtureAssertions
{
    public static IReadOnlyList<TEvent> PublishedEventsOfType<TEvent>(this PhantomTestFixture fixture)
        where TEvent : IIntegrationEvent
    {
        return fixture.GetPublishedEvents<TEvent>();
    }

    public static bool HasPublishedEvent<TEvent>(this PhantomTestFixture fixture, Func<TEvent, bool>? predicate = null)
        where TEvent : IIntegrationEvent
    {
        var events = fixture.GetPublishedEvents<TEvent>();
        return predicate is null ? events.Count > 0 : events.Any(predicate);
    }

    public static TEvent? GetFirstPublishedEvent<TEvent>(this PhantomTestFixture fixture)
        where TEvent : IIntegrationEvent
    {
        return fixture.GetPublishedEvents<TEvent>().FirstOrDefault();
    }

    public static void ShouldHavePublishedEvent<TEvent>(this PhantomTestFixture fixture, Func<TEvent, bool>? predicate = null, string? message = null)
        where TEvent : IIntegrationEvent
    {
        if (!HasPublishedEvent(fixture, predicate))
        {
            var msg = message ?? $"Expected at least one published event of type {typeof(TEvent).Name}.";
            throw new Xunit.Sdk.XunitException(msg);
        }
    }

    public static void ShouldHavePublishedEventCount<TEvent>(this PhantomTestFixture fixture, int expected, string? message = null)
        where TEvent : IIntegrationEvent
    {
        var actual = fixture.GetPublishedEvents<TEvent>().Count;
        if (actual != expected)
        {
            var msg = message ?? $"Expected {expected} events of type {typeof(TEvent).Name}, but found {actual}.";
            throw new Xunit.Sdk.XunitException(msg);
        }
    }

    public static void ShouldNotHavePublishedAnyEvent(this PhantomTestFixture fixture, string? message = null)
    {
        var actual = fixture.GetPublishedEvents().Count;
        if (actual > 0)
        {
            var msg = message ?? $"Expected no published events, but found {actual}.";
            throw new Xunit.Sdk.XunitException(msg);
        }
    }
}

public class InMemoryEventRecorder : IIntegrationEventHandler<IIntegrationEvent>
{
    private readonly ConcurrentQueue<IIntegrationEvent> _events = new();

    public IReadOnlyList<IIntegrationEvent> Events => _events.ToList();

    public Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }

    public void Clear() => _events.Clear();
}

public class InMemoryEventRecorder<TEvent> : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly ConcurrentQueue<TEvent> _events = new();

    public IReadOnlyList<TEvent> Events => _events.ToList();

    public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }

    public void Clear() => _events.Clear();
}
