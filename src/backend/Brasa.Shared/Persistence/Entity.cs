namespace Brasa.Shared.Persistence;

/// <summary>Anything with an identity that is persisted.</summary>
public interface IEntity
{
    /// <summary>The primary key.</summary>
    Guid Id { get; }
}

/// <summary>
/// An entity that belongs to exactly one tenant and is subject to row-level security.
/// </summary>
/// <remarks>
/// Implementing this is what opts a table into the global query filter and the
/// RLS policy. A tenant-scoped table that forgets it is a data leak, so
/// <c>TenantIsolationTests</c> asserts by reflection that every entity in every
/// module implements this or is on an explicit allow-list of shared reference data.
/// </remarks>
public interface ITenantOwned
{
    /// <summary>The owning organization. Never changes after insert.</summary>
    Guid TenantId { get; }
}

/// <summary>Records who created and last changed a row, and when.</summary>
/// <remarks>
/// Distinct from the fiscal audit log. This is ordinary bookkeeping; fiscal
/// immutability is enforced separately and far more strictly.
/// </remarks>
public interface IAuditable
{
    /// <summary>When the row was inserted, in UTC.</summary>
    DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>The user who inserted the row, when known.</summary>
    Guid? CreatedBy { get; set; }

    /// <summary>When the row was last updated, in UTC.</summary>
    DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <summary>The user who last updated the row, when known.</summary>
    Guid? ModifiedBy { get; set; }
}

/// <summary>
/// Marks a row as removed without deleting it.
/// </summary>
/// <remarks>
/// Menu items and staff records are soft-deleted because historical orders and
/// fiscal documents reference them and must stay readable for the ten years
/// Portuguese law requires those records be retained.
/// </remarks>
public interface ISoftDeletable
{
    /// <summary>When the row was soft-deleted, or null while it is active.</summary>
    DateTimeOffset? DeletedAtUtc { get; set; }
}

/// <summary>
/// Base class for tenant-owned, audited entities.
/// </summary>
/// <remarks>
/// <para>
/// Ids are UUIDv7: time-ordered, so they cluster well in PostgreSQL indexes
/// instead of scattering writes across the B-tree the way random UUIDv4 does.
/// </para>
/// <para>
/// Equally important for this system, they can be generated <b>offline</b>. A POS
/// terminal with no connectivity mints its own order ids and syncs them later
/// without a round trip to the database and without collision risk.
/// </para>
/// </remarks>
public abstract class Entity : IEntity, ITenantOwned, IAuditable
{
    /// <summary>Creates an entity with a freshly generated UUIDv7.</summary>
    protected Entity() => Id = Guid.CreateVersion7();

    /// <summary>Rehydrates an entity with a known id. Used by EF Core and by sync.</summary>
    protected Entity(Guid id) => Id = id;

    /// <inheritdoc/>
    public Guid Id { get; protected set; }

    /// <inheritdoc/>
    public Guid TenantId { get; protected set; }

    /// <inheritdoc/>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc/>
    public Guid? CreatedBy { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc/>
    public Guid? ModifiedBy { get; set; }

    /// <summary>
    /// Assigns the owning tenant. Called by the DbContext on insert; the value is
    /// fixed for the lifetime of the row.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entity already belongs to a tenant.</exception>
    public void AssignTenant(Guid tenantId)
    {
        if (TenantId != Guid.Empty && TenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"Entity {GetType().Name}/{Id} already belongs to tenant {TenantId} and cannot be reassigned.");
        }

        TenantId = tenantId;
    }
}
