using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Data.EfCore;
using Phantom.Data.Interceptors;
using Phantom.Data.Outbox;
using Phantom.Data.Services;
using Phantom.Data.Specifications;
using Phantom.Messaging.Outbox;

namespace Phantom.Data.Extensions;

/// <summary>
/// Extension methods for registering Phantom.Data services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Phantom.Data services, including the DbContext, interceptors,
    /// repositories, and the domain event dispatcher.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="configure">An action to configure <see cref="PhantomDataOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPhantomData(this IServiceCollection services, Action<PhantomDataOptions> configure)
    {
        var options = new PhantomDataOptions();
        configure(options);
        options.Validate();

        services.AddSingleton<ISpecificationEvaluator, EfSpecificationEvaluator>();

        services.AddDbContext<PhantomDbContext>((sp, dbOptions) =>
        {
            var interceptors = new List<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>();

            if (options.UseSoftDelete)
            {
                var currentUserService = sp.GetService<ICurrentUserService>();
                interceptors.Add(new SoftDeleteInterceptor(currentUserService));
            }

            if (options.UseAuditable)
            {
                interceptors.Add(new AuditableInterceptor(sp.GetService<ICurrentUserService>()));
            }

            if (options.ConfigureDbContext != null)
            {
                options.ConfigureDbContext(dbOptions);
            }
            else
            {
                switch (options.Provider)
                {
                    case DatabaseProvider.PostgreSQL:
                        dbOptions.UseNpgsql(options.ConnectionString, npgsqlOptions =>
                        {
                            if (!string.IsNullOrEmpty(options.MigrationsAssembly))
                                npgsqlOptions.MigrationsAssembly(options.MigrationsAssembly);
                        });
                        break;
                    case DatabaseProvider.SqlServer:
                        dbOptions.UseSqlServer(options.ConnectionString, sqlOptions =>
                        {
                            if (!string.IsNullOrEmpty(options.MigrationsAssembly))
                                sqlOptions.MigrationsAssembly(options.MigrationsAssembly);
                        });
                        break;
                    case DatabaseProvider.InMemory:
                        dbOptions.UseInMemoryDatabase("PhantomDb");
                        break;
                }
            }

            if (interceptors.Any())
                dbOptions.AddInterceptors(interceptors);
        });

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

        if (options.UseOutbox)
        {
            services.AddScoped<IOutboxMessageRepository, EfOutboxRepository>();
        }

        // Use TryAddScoped so this doesn't override a user-registered ICurrentUserService
        services.TryAddScoped<ICurrentUserService, DefaultCurrentUserService>();

        return services;
    }
}

/// <summary>
/// Default implementation of <see cref="ICurrentUserService"/> that returns <c>null</c>.
/// Can be overridden by registering your own <see cref="ICurrentUserService"/> implementation
/// before calling <see cref="ServiceCollectionExtensions.AddPhantomData"/>.
/// </summary>
internal class DefaultCurrentUserService : ICurrentUserService
{
    /// <inheritdoc/>
    public string? GetCurrentUserId() => null;
}
