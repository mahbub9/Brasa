using Brasa.Modules.Floor.Domain;
using Brasa.Modules.Floor.Persistence;
using Brasa.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Seed;

/// <summary>
/// Seeds a demo floor plan for <see cref="DevTenant"/> on startup.
/// </summary>
/// <remarks>
/// Stands in for FLR's drag-and-drop floor editor (FLR-03, not built yet) and
/// for real tenant onboarding (IDN-13) — same role <see cref="DevCatalogSeeder"/>
/// plays for the menu. Guarded the same way: never in Production.
/// </remarks>
public static class DevFloorSeeder
{
    /// <summary>Seeds the demo floor plan if <see cref="DevTenant"/> has none yet. Idempotent.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="environment"/> reports Production.</exception>
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("DevFloorSeeder must never run in Production.");
        }

        await using var scope = services.CreateAsyncScope();

        // Seeding happens outside any HTTP request — resolve the tenant here,
        // before touching the DbContext, the same way DevCatalogSeeder does.
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(DevTenant.Id);

        var db = scope.ServiceProvider.GetRequiredService<FloorDbContext>();

        if (await db.Rooms.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var salao = new Room("Salão", 1);
        var esplanada = new Room("Esplanada", 2);

        db.Rooms.AddRange(salao, esplanada);

        // 16 tables, not 8 — the original count. E2E has grown past twenty
        // tests sharing this seeded floor plan (src/web/e2e), and back-to-back
        // full runs started occasionally exhausting an 8-table pool under
        // real 2-worker parallelism (a QA-02 scaling limitation, not a
        // product bug — see docs/development/e2e-testing.md). Doubling the
        // pool is a cheap mitigation; a disposable-per-run database is the
        // real fix, tracked separately.
        db.Tables.AddRange(
            new Table(salao.Id, "Mesa 1", seats: 2, positionX: 0, positionY: 0, TableShape.Round),
            new Table(salao.Id, "Mesa 2", seats: 2, positionX: 1, positionY: 0, TableShape.Round),
            new Table(salao.Id, "Mesa 3", seats: 4, positionX: 2, positionY: 0, TableShape.Square),
            new Table(salao.Id, "Mesa 4", seats: 4, positionX: 0, positionY: 1, TableShape.Square),
            new Table(salao.Id, "Mesa 5", seats: 6, positionX: 1, positionY: 1, TableShape.Rectangle),
            new Table(salao.Id, "Mesa 9", seats: 2, positionX: 2, positionY: 1, TableShape.Round),
            new Table(salao.Id, "Mesa 10", seats: 4, positionX: 0, positionY: 2, TableShape.Square),
            new Table(salao.Id, "Mesa 11", seats: 4, positionX: 1, positionY: 2, TableShape.Square),
            new Table(salao.Id, "Mesa 12", seats: 6, positionX: 2, positionY: 2, TableShape.Rectangle),
            new Table(esplanada.Id, "Mesa 6", seats: 2, positionX: 0, positionY: 0, TableShape.Round),
            new Table(esplanada.Id, "Mesa 7", seats: 2, positionX: 1, positionY: 0, TableShape.Round),
            new Table(esplanada.Id, "Mesa 8", seats: 4, positionX: 2, positionY: 0, TableShape.Rectangle),
            new Table(esplanada.Id, "Mesa 13", seats: 2, positionX: 0, positionY: 1, TableShape.Round),
            new Table(esplanada.Id, "Mesa 14", seats: 2, positionX: 1, positionY: 1, TableShape.Round),
            new Table(esplanada.Id, "Mesa 15", seats: 4, positionX: 2, positionY: 1, TableShape.Rectangle),
            new Table(esplanada.Id, "Mesa 16", seats: 6, positionX: 0, positionY: 2, TableShape.Rectangle));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
