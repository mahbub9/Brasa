using Brasa.Modules.Ordering.Domain;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Modules.Ordering.Persistence;

/// <summary>Owns the <c>ordering</c> schema: orders and their lines.</summary>
public sealed class OrderingDbContext(
    DbContextOptions<OrderingDbContext> options,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantContextAccessor,
    IClock clock)
    : TenantAwareDbContext(options, tenantContext, tenantContextAccessor, clock)
{
    /// <inheritdoc/>
    protected override string Schema => "ordering";

    /// <summary>Orders.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        if (!Database.IsNpgsql())
        {
            // OrderConfiguration maps Order's optimistic-concurrency token
            // (ORD-21) to "xmin" — Postgres's built-in row-version system
            // column. SQLite (the beta's provider, ADR 0012) has no such
            // column, which fails every insert with a NOT NULL violation.
            // Dropped here for any non-Postgres provider — the exact same
            // trade-down FloorDbContext already makes for Table's own xmin.
            modelBuilder.Entity<Order>().Ignore("xmin");
        }
    }
}
