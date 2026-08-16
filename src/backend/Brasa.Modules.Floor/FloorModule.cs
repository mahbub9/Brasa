using Brasa.Modules.Floor.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Floor;

/// <summary>Registers the Floor module's persistence.</summary>
public static class FloorModule
{
    /// <summary>
    /// Adds <see cref="FloorDbContext"/> against whichever provider
    /// <paramref name="databaseOptions"/> selects — see
    /// <see cref="ModulePersistenceExtensions.AddModuleDbContext{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddFloorModule(
        this IServiceCollection services, DatabaseOptions databaseOptions, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddModuleDbContext<FloorDbContext>(databaseOptions, connectionString, "floor");
    }
}
