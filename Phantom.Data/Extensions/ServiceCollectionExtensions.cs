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

public static class ServiceCollectionExtensions
{
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

        services.TryAddScoped<ICurrentUserService, DefaultCurrentUserService>();

        return services;
    }
}

internal class DefaultCurrentUserService : ICurrentUserService
{
    public string? GetCurrentUserId() => null;
}
