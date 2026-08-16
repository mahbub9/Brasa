using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Catalog;

/// <summary>Registers the Catalog module's persistence.</summary>
public static class CatalogModule
{
    /// <summary>
    /// Adds <see cref="CatalogDbContext"/> against whichever provider
    /// <paramref name="databaseOptions"/> selects — see
    /// <see cref="ModulePersistenceExtensions.AddModuleDbContext{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        DatabaseOptions databaseOptions,
        string? connectionString,
        string? systemConnectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddModuleDbContext<CatalogDbContext>(
            databaseOptions, connectionString, "catalog", systemConnectionString);
    }
}
