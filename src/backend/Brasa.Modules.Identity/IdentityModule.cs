using Brasa.Modules.Identity.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Identity;

/// <summary>Registers the Identity module's persistence.</summary>
public static class IdentityModule
{
    /// <summary>
    /// Adds <see cref="IdentityDbContext"/>, wired with the shared
    /// <see cref="TenantSessionInterceptor"/> so its connections carry the
    /// current tenant into PostgreSQL's row-level security policies.
    /// </summary>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<IdentityDbContext>((sp, options) =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"))
                .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
