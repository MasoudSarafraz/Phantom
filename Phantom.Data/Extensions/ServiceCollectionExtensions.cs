using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Data.EfCore;
using Phantom.Data.Interceptors;
using Phantom.Data.Outbox;
using Phantom.Data.Specifications;
using Phantom.Messaging.Outbox;
using System.Reflection;

namespace Phantom.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPhantomData(this IServiceCollection services, Action<PhantomDataOptions> configure)
    {
        var options = new PhantomDataOptions();
        configure(options);

        services.AddSingleton<ISpecificationEvaluator, EfSpecificationEvaluator>();

        services.AddDbContext<PhantomDbContext>((sp, dbOptions) =>
        {
            var interceptors = new List<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>();
            if (options.UseSoftDelete) interceptors.Add(new SoftDeleteInterceptor());
            if (options.UseAuditable) interceptors.Add(new AuditableInterceptor(sp.GetService<ICurrentUserService>()));

            if (options.ConfigureDbContext != null) { options.ConfigureDbContext(dbOptions); }
            else
            {
                switch (options.Provider)
                {
                    case DatabaseProvider.PostgreSQL: dbOptions.UseNpgsql(options.ConnectionString); break;
                    case DatabaseProvider.SqlServer: dbOptions.UseSqlServer(options.ConnectionString); break;
                    case DatabaseProvider.InMemory: dbOptions.UseInMemoryDatabase("PhantomDb"); break;
                }
            }
            if (interceptors.Any()) dbOptions.AddInterceptors(interceptors);
        });

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        if (options.UseOutbox) services.AddScoped<IOutboxMessageRepository, EfOutboxRepository>();
        if (options.UseAuditable) services.AddScoped<ICurrentUserService, DefaultCurrentUserService>();
        return services;
    }
}

internal class DefaultCurrentUserService : ICurrentUserService { public string? GetCurrentUserId() => null; }
