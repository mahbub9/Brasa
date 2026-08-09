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
    }
}
