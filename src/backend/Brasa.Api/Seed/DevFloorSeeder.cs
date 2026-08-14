using Brasa.Modules.Floor.Domain;
using Brasa.Modules.Floor.Persistence;
using Brasa.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Seed;

/// <summary>
/// Seeds a demo floor plan for <see cref="DevTenant"/> on startup.
/// </summary>
/// <remarks>
/// Stands in for real tenant onboarding (IDN-13, not built yet) — same role
/// <see cref="DevCatalogSeeder"/> plays for the menu. FLR-03's own editor
/// (table/room CRUD plus the drag-and-drop canvas) is built and lets a
/// tenant reshape this seeded starting point; this seeder is what a brand
/// new tenant would otherwise stare at an empty floor plan without.
/// Guarded the same way: never in Production.
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

        // 32 tables, not 16. E2E has grown from ~23 tests sharing an 8-table
        // pool (when this was first doubled) to 185 sharing 16 — a worse
        // tests-per-table ratio than the one that originally exhausted the
        // pool under real 2-worker parallelism (a QA-02 scaling limitation,
        // not a product bug — see docs/development/e2e-testing.md). Doubling
        // again is the same cheap mitigation that doc's own "what's next"
        // section names; a disposable-per-run database is the real fix,
        // tracked separately. A 4x4 grid per room, shape and seat count
        // cycling for visual variety (matters for FLR-03's canvas).
        var shapes = new[] { TableShape.Round, TableShape.Square, TableShape.Rectangle };
        var seatCounts = new[] { 2, 4, 6, 4 };
        var tables = new List<Table>();
        var mesaNumber = 1;

        foreach (var room in new[] { salao, esplanada })
        {
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    var index = tables.Count;
                    tables.Add(new Table(
                        room.Id,
                        $"Mesa {mesaNumber}",
                        seats: seatCounts[index % seatCounts.Length],
                        positionX: x,
                        positionY: y,
                        shapes[index % shapes.Length]));
                    mesaNumber++;
                }
            }
        }

        db.Tables.AddRange(tables);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
