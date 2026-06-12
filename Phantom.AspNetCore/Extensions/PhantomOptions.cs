using Phantom.Data.Extensions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.RabbitMq;

namespace Phantom.AspNetCore.Extensions;

public class PhantomOptions
{
    internal PhantomDataOptions DataOptions { get; } = new();
    internal PhantomMessagingOptions MessagingOptions { get; } = new();
    internal bool UseCQRS { get; set; } = true;
    internal bool UseValidation { get; set; }

    public PhantomOptions UsePostgreSQL(string connectionString, Action<PhantomDataOptions>? configure = null) { DataOptions.Provider = DatabaseProvider.PostgreSQL; DataOptions.ConnectionString = connectionString; configure?.Invoke(DataOptions); return this; }
    public PhantomOptions UseSqlServer(string connectionString, Action<PhantomDataOptions>? configure = null) { DataOptions.Provider = DatabaseProvider.SqlServer; DataOptions.ConnectionString = connectionString; configure?.Invoke(DataOptions); return this; }
    public PhantomOptions UseInMemoryDatabase(Action<PhantomDataOptions>? configure = null) { DataOptions.Provider = DatabaseProvider.InMemory; configure?.Invoke(DataOptions); return this; }
    public PhantomOptions UseRabbitMq(string host, Action<RabbitMqOptions>? configure = null) { MessagingOptions.AddChannel("default", c => c.UseRabbitMq(r => { r.Host = host; configure?.Invoke(r); })); return this; }
    public PhantomOptions AddChannel(string name, Action<ChannelBuilder> configure) { MessagingOptions.AddChannel(name, configure); return this; }
    public PhantomOptions UseFluentValidation() { UseValidation = true; return this; }
    public PhantomOptions UseSoftDelete() { DataOptions.UseSoftDelete = true; return this; }
    public PhantomOptions UseAuditable() { DataOptions.UseAuditable = true; return this; }
    public PhantomOptions UseOutbox() { DataOptions.UseOutbox = true; MessagingOptions.UseOutboxProcessing(); return this; }
    public PhantomOptions RouteEvent<TEvent>(params string[] channelNames) where TEvent : Core.Events.IIntegrationEvent { MessagingOptions.RouteEvent<TEvent>(channelNames); return this; }
    public PhantomOptions EnableIdempotency() { MessagingOptions.EnableIdempotency(); return this; }
    public PhantomOptions ConfigureRetry(int maxRetries = 3, TimeSpan? baseDelay = null) { MessagingOptions.ConfigureRetry(maxRetries, baseDelay); return this; }
    public PhantomOptions ConfigureCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null) { MessagingOptions.ConfigureCircuitBreaker(failureThreshold, resetTimeout); return this; }
}
