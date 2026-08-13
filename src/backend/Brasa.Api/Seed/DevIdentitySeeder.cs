using Brasa.Modules.Identity.Domain;
using Brasa.Modules.Identity.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Seed;

/// <summary>
/// Seeds a demo Organization / Site / Terminal, and two staff PIN accounts
/// (IDN-08/09), for <see cref="DevTenant"/> on startup (IDN-01).
/// </summary>
/// <remarks>
/// Stands in for real tenant onboarding (IDN-13, not built yet) — same role
/// <see cref="DevFloorSeeder"/> plays for the floor plan. Guarded the same
/// way: never in Production.
/// </remarks>
public static class DevIdentitySeeder
{
    /// <summary>Seeds the demo organization/site/terminal and staff if <see cref="DevTenant"/> doesn't have them yet. Idempotent.</summary>
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

        var organization = await db.Organizations.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            organization = new Organization("Brasa Demo, Lda");
            db.Organizations.Add(organization);

            var site = new Site(organization.Id, "Restaurante Central", PortugueseRegion.Continental);
            db.Sites.Add(site);

            var terminal = new Terminal(site.Id, "Caixa 1");
            db.Terminals.Add(terminal);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Independent of whether the organization above was just created or
        // already existed — a seeding step added *after* an early return
        // gated on "no organizations yet" would silently never run on this
        // session's own long-lived dev database, the exact trap CAT-07/08's
        // tax-rule seeding hit once already (see docs/ai/README.md).
        await SeedStaffAsync(db, organization.Id, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedStaffAsync(IdentityDbContext db, Guid organizationId, CancellationToken cancellationToken)
    {
        if (await db.Staff.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var site = await db.Sites
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (site is null)
        {
            // Every organization gets a site above — this would mean the
            // demo data is in an inconsistent state, not a case to seed
            // staff into. Leave it for whatever surfaced that to be noticed
            // some other way, rather than guessing a site to attach to.
            return;
        }

        // One of each role, enough to prove both a Manager and an ordinary
        // Staff PIN both work — not a claim that a real restaurant has
        // exactly two staff members.
        db.Staff.Add(new Staff(site.Id, "Ana Ferreira", StaffRole.Manager, "1234"));
        db.Staff.Add(new Staff(site.Id, "Tiago Costa", StaffRole.Staff, "5678"));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
