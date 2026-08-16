using Brasa.Modules.Floor.Domain;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Modules.Floor.Persistence;

/// <summary>
/// Owns the <c>floor</c> schema: rooms and tables.
/// </summary>
public sealed class FloorDbContext(
    DbContextOptions<FloorDbContext> options,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantContextAccessor,
    IClock clock)
    : TenantAwareDbContext(options, tenantContext, tenantContextAccessor, clock)
{
    /// <inheritdoc/>
    protected override string Schema => "floor";

    /// <summary>Rooms / areas.</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>Tables.</summary>
    public DbSet<Table> Tables => Set<Table>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FloorDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        if (!Database.IsNpgsql())
        {
            // TableConfiguration maps Table's optimistic-concurrency token to
            // "xmin" — Postgres's built-in row-version system column, which
            // exists on every row with no migration and no app-side write
            // ever needed. SQLite (the beta's provider, ADR 0012) has no
            // such column and no way to generate one, which fails every
            // insert with a NOT NULL violation. Dropped here for any
            // non-Postgres provider: a documented trade-down, the same
            // class of decision as ADR 0012's RLS drop — a single-restaurant
            // pilot has far lower concurrent-occupy pressure than production
            // multi-terminal use, so losing the compare-and-swap guard on
            // Table.Occupy() is acceptable there, not on Postgres.
            modelBuilder.Entity<Table>().Ignore("xmin");
        }
    }
}
