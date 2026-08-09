using Brasa.Modules.Floor.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Floor;

/// <summary>Registers the Floor module's persistence.</summary>
public static class FloorModule
{
    /// <summary>
    /// Adds <see cref="FloorDbContext"/>, wired with the shared
    /// <see cref="TenantSessionInterceptor"/> so its connections carry the
    /// current tenant into PostgreSQL's row-level security policies.
    /// </summary>
    public static IServiceCollection AddFloorModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FloorDbContext>((sp, options) =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "floor"))
                .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
