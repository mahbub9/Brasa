using Brasa.Modules.Payments.Persistence;
using Brasa.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Modules.Payments;

/// <summary>Registers the Payments module's persistence.</summary>
public static class PaymentsModule
{
    /// <summary>
    /// Adds <see cref="PaymentsDbContext"/> against whichever provider
    /// <paramref name="databaseOptions"/> selects — see
    /// <see cref="ModulePersistenceExtensions.AddModuleDbContext{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        DatabaseOptions databaseOptions,
        string? connectionString,
        string? systemConnectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddModuleDbContext<PaymentsDbContext>(
            databaseOptions, connectionString, "payments", systemConnectionString);
    }
}
