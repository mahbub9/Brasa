using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Brasa.Shared.Persistence;

/// <summary>
/// Base <see cref="DbContext"/> for every module. Applies tenant assignment,
/// audit stamping and the global query filters.
/// </summary>
/// <remarks>
/// <para>
/// Each module owns its own context and its own PostgreSQL schema. Modules never
/// map another module's entities — see
/// <c>docs/architecture/module-boundaries.md</c>.
/// </para>
/// <para>
/// Isolation is enforced in two independent places: the query filter added here,
/// and the row-level security policy applied in migrations. Only the second is a
/// security boundary.
/// </para>
/// </remarks>
public abstract class TenantAwareDbContext(
    DbContextOptions options,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantContextAccessor,
    IClock clock)
    : DbContext(options)
{
    /// <summary>The PostgreSQL schema this module's tables live in.</summary>
    protected abstract string Schema { get; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyTenantQueryFilters(tenantContextAccessor);
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Assigns the owning tenant on insert and maintains the audit columns.
    /// </summary>
    /// <remarks>
    /// Tenant assignment happens here rather than at call sites so that no caller
    /// can forget it, and <see cref="Entity.AssignTenant"/> refuses to move an
    /// entity between tenants.
    /// </remarks>
    private void StampEntities()
    {
        var now = clock.UtcNow;
        var userId = tenantContext.UserId;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.AssignTenant(tenantContext.TenantId);
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = now;
                    entry.Entity.ModifiedBy = userId;

                    // An issued fiscal document must never be mutated, and tenancy
                    // must never move. Both are guarded at the boundary rather than
                    // trusted to callers.
                    PreventTenantReassignment(entry);
                    break;

                case EntityState.Deleted:
                    PreventHardDeleteOfSoftDeletable(entry);
                    break;

                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }
    }

    private static void PreventTenantReassignment(EntityEntry<Entity> entry)
    {
        var tenantProperty = entry.Property(e => e.TenantId);

        if (tenantProperty.IsModified)
        {
            throw new InvalidOperationException(
                $"Attempted to move {entry.Entity.GetType().Name}/{entry.Entity.Id} between tenants. " +
                "Tenant ownership is fixed for the lifetime of a row.");
        }
    }

    /// <summary>
    /// A row implementing <see cref="ISoftDeletable"/> must never actually be
    /// removed — historical orders and fiscal documents may still reference it,
    /// and Portuguese law requires those stay readable for ten years. Calling
    /// <c>DbSet.Remove</c> on one is a bug, not a valid way to delete it; the
    /// domain method that sets <see cref="ISoftDeletable.DeletedAtUtc"/> and
    /// saves as an ordinary update is the only sanctioned path.
    /// </summary>
    private static void PreventHardDeleteOfSoftDeletable(EntityEntry<Entity> entry)
    {
        if (entry.Entity is ISoftDeletable)
        {
            throw new InvalidOperationException(
                $"{entry.Entity.GetType().Name}/{entry.Entity.Id} implements ISoftDeletable and must never be " +
                "hard-deleted. Call its soft-delete domain method instead, which sets DeletedAtUtc and saves as an update.");
        }
    }
}
