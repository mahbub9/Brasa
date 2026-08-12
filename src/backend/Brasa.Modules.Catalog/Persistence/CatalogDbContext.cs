using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Modules.Catalog.Persistence;

/// <summary>
/// Owns the <c>catalog</c> schema: menu categories and items.
/// </summary>
public sealed class CatalogDbContext(
    DbContextOptions<CatalogDbContext> options,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantContextAccessor,
    IClock clock)
    : TenantAwareDbContext(options, tenantContext, tenantContextAccessor, clock)
{
    /// <inheritdoc/>
    protected override string Schema => "catalog";

    /// <summary>Menu categories.</summary>
    public DbSet<MenuCategory> Categories => Set<MenuCategory>();

    /// <summary>Menu items.</summary>
    public DbSet<MenuItem> Items => Set<MenuItem>();

    /// <summary>Per-site price lists (CAT-05).</summary>
    public DbSet<PriceList> PriceLists => Set<PriceList>();

    /// <summary>Fixed-price bundles of menu items (CAT-10).</summary>
    public DbSet<Combo> Combos => Set<Combo>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
