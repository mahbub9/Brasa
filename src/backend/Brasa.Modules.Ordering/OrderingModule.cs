using Brasa.Modules.Ordering.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Ordering;

/// <summary>Registers the Ordering module's persistence.</summary>
public static class OrderingModule
{
    /// <summary>
    /// Adds <see cref="OrderingDbContext"/> against whichever provider
    /// <paramref name="databaseOptions"/> selects — see
    /// <see cref="ModulePersistenceExtensions.AddModuleDbContext{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddOrderingModule(
        this IServiceCollection services, DatabaseOptions databaseOptions, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddModuleDbContext<OrderingDbContext>(databaseOptions, connectionString, "ordering");
    }
}
