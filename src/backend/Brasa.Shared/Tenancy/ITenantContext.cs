namespace Brasa.Shared.Tenancy;

/// <summary>
/// Who the current request belongs to. Resolved once per request from the caller's
/// token and then treated as immutable.
/// </summary>
/// <remarks>
/// <para>
/// Every tenant-owned table is filtered by <see cref="TenantId"/> in two
/// independent places: an EF Core global query filter, and a PostgreSQL
/// row-level security policy. The RLS policy is the one that actually protects
/// the data — the query filter is a convenience that a raw SQL query or a
/// forgotten <c>IgnoreQueryFilters()</c> can bypass.
/// </para>
/// <para>See <c>docs/architecture/multi-tenancy.md</c>.</para>
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// The organization (customer) the request belongs to. This is the isolation
    /// boundary — data never crosses it.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// The restaurant location, when the caller is scoped to one. Null for
    /// organization-wide back-office calls.
    /// </summary>
    Guid? SiteId { get; }

    /// <summary>The POS terminal, when the call came from one.</summary>
    Guid? TerminalId { get; }

    /// <summary>The authenticated user, when there is one.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// True for background jobs and migrations that legitimately operate across
    /// tenants, such as the monthly SAF-T submission sweep.
    /// </summary>
    /// <remarks>
    /// Setting this bypasses the RLS policy via a privileged database role. It must
    /// never be reachable from an HTTP request path.
    /// </remarks>
    bool IsSystemContext { get; }

    /// <summary>True when a tenant has been resolved for this scope.</summary>
    bool HasTenant => TenantId != Guid.Empty;
}
