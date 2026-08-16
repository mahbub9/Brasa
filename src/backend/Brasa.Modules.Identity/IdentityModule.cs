using Brasa.Modules.Identity.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Identity;

/// <summary>Registers the Identity module's persistence.</summary>
public static class IdentityModule
{
    /// <summary>
    /// Adds <see cref="IdentityDbContext"/> against whichever provider
    /// <paramref name="databaseOptions"/> selects — see
    /// <see cref="ModulePersistenceExtensions.AddModuleDbContext{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, DatabaseOptions databaseOptions, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddModuleDbContext<IdentityDbContext>(databaseOptions, connectionString, "identity");
    }
}
