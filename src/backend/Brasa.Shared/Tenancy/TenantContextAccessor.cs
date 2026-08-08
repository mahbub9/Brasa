namespace Brasa.Shared.Tenancy;

/// <summary>
/// Supplies the current tenant to code that cannot hold a scoped dependency —
/// principally EF Core global query filters.
/// </summary>
/// <remarks>
/// A query filter expression is compiled into EF Core's model, which is cached
/// for the lifetime of the application rather than rebuilt per request. Closing
/// over a scoped <see cref="ITenantContext"/> directly would therefore capture
/// whichever request happened to trigger the first model build, and every later
/// request — every other tenant — would silently read that tenant's rows.
/// </remarks>
public interface ITenantContextAccessor
{
    /// <summary>The tenant visible to the current async call chain.</summary>
    Guid CurrentTenantId { get; }
}

/// <summary>
/// Singleton <see cref="ITenantContextAccessor"/> backed by an
/// <see cref="AsyncLocal{T}"/>.
/// </summary>
/// <remarks>
/// The fix for the caching trap described on <see cref="ITenantContextAccessor"/>
/// is to filter through a stable singleton whose <b>property value</b>, not the
/// object itself, changes per request. <see cref="AsyncLocal{T}"/> gives that
/// value request-scoped flow across the async call chain without touching DI
/// scoping at all.
/// <para>
/// <see cref="TenantContext"/> pushes into this accessor from
/// <see cref="TenantContext.Resolve"/>, so application code keeps using the
/// ordinary scoped <see cref="ITenantContext"/> and never touches this type
/// directly.
/// </para>
/// </remarks>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<Guid> Current = new();

    /// <inheritdoc/>
    public Guid CurrentTenantId => Current.Value;

    /// <summary>
    /// Sets the tenant visible to query filters for the remainder of this async
    /// call chain. Called once, by <see cref="TenantContext.Resolve"/>.
    /// </summary>
    internal static void SetCurrentTenant(Guid tenantId) => Current.Value = tenantId;
}
