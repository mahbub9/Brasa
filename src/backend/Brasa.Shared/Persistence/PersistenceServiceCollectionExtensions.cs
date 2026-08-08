using Brasa.Shared.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Shared.Persistence;

/// <summary>
/// Registers the tenancy and persistence primitives every module's
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> depends on.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="ITenantContext"/> (scoped), <see cref="ITenantContextAccessor"/>
    /// (singleton) and <see cref="TenantSessionInterceptor"/> (scoped).
    /// </summary>
    /// <remarks>
    /// Call once from the host (<c>Brasa.Api</c>, <c>Brasa.SiteAgent</c>), then
    /// register each module's <c>DbContext</c> with
    /// <c>options.AddInterceptors(serviceProvider.GetRequiredService&lt;TenantSessionInterceptor&gt;())</c>.
    /// </remarks>
    public static IServiceCollection AddBrasaTenancy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<TenantSessionInterceptor>();

        return services;
    }
}
