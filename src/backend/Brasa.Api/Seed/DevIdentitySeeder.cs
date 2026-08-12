using Brasa.Modules.Identity.Domain;
using Brasa.Modules.Identity.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Seed;

/// <summary>
/// Seeds a demo Organization / Site / Terminal for <see cref="DevTenant"/> on
/// startup (IDN-01).
/// </summary>
/// <remarks>
/// Stands in for real tenant onboarding (IDN-13, not built yet) — same role
/// <see cref="DevFloorSeeder"/> plays for the floor plan. Guarded the same
/// way: never in Production.
/// </remarks>
public static class DevIdentitySeeder
{
    /// <summary>Seeds the demo organization/site/terminal if <see cref="DevTenant"/> has none yet. Idempotent.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="environment"/> reports Production.</exception>
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("DevIdentitySeeder must never run in Production.");
        }

        await using var scope = services.CreateAsyncScope();

        // Seeding happens outside any HTTP request — resolve the tenant here,
        // before touching the DbContext, the same way DevFloorSeeder does.
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(DevTenant.Id);

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        if (await db.Organizations.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var organization = new Organization("Brasa Demo, Lda");
        db.Organizations.Add(organization);

        var site = new Site(organization.Id, "Restaurante Central", PortugueseRegion.Continental);
        db.Sites.Add(site);

        var terminal = new Terminal(site.Id, "Caixa 1");
        db.Terminals.Add(terminal);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
