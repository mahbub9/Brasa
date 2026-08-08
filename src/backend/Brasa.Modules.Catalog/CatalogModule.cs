using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Catalog;

/// <summary>Registers the Catalog module's persistence.</summary>
public static class CatalogModule
{
    /// <summary>
    /// Adds <see cref="CatalogDbContext"/>, wired with the shared
    /// <see cref="TenantSessionInterceptor"/> so its connections carry the
    /// current tenant into PostgreSQL's row-level security policies.
    /// </summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<CatalogDbContext>((sp, options) =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog"))
                .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
