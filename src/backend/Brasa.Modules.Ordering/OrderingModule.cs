using Brasa.Modules.Ordering.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Ordering;

/// <summary>Registers the Ordering module's persistence.</summary>
public static class OrderingModule
{
    /// <summary>Adds <see cref="OrderingDbContext"/>, wired with the shared tenant session interceptor.</summary>
    public static IServiceCollection AddOrderingModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<OrderingDbContext>((sp, options) =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "ordering"))
                .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
