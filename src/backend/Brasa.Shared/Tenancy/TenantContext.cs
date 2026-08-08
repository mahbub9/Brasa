namespace Brasa.Shared.Tenancy;

/// <summary>
/// Mutable-once implementation of <see cref="ITenantContext"/>, registered as
/// scoped and populated by the tenant-resolution middleware.
/// </summary>
/// <remarks>
/// It is settable exactly once per scope. A second attempt throws, so a bug that
/// tries to switch tenants mid-request fails loudly instead of silently serving
/// one customer's data to another.
/// </remarks>
public sealed class TenantContext : ITenantContext
{
    private bool _resolved;

    /// <inheritdoc/>
    public Guid TenantId { get; private set; }

    /// <inheritdoc/>
    public Guid? SiteId { get; private set; }

    /// <inheritdoc/>
    public Guid? TerminalId { get; private set; }

    /// <inheritdoc/>
    public Guid? UserId { get; private set; }

    /// <inheritdoc/>
    public bool IsSystemContext { get; private set; }

    /// <summary>
    /// Binds this scope to a tenant. Called by the tenant-resolution middleware.
    /// </summary>
    /// <exception cref="InvalidOperationException">The scope is already bound.</exception>
    public void Resolve(Guid tenantId, Guid? siteId = null, Guid? terminalId = null, Guid? userId = null)
    {
        EnsureUnresolved();

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        TenantId = tenantId;
        SiteId = siteId;
        TerminalId = terminalId;
        UserId = userId;
        _resolved = true;
    }

    /// <summary>
    /// Binds this scope to the privileged cross-tenant context used by background
    /// jobs. Must never be called from a request path.
    /// </summary>
    /// <exception cref="InvalidOperationException">The scope is already bound.</exception>
    public void ResolveAsSystem()
    {
        EnsureUnresolved();

        IsSystemContext = true;
        _resolved = true;
    }

    private void EnsureUnresolved()
    {
        if (_resolved)
        {
            throw new InvalidOperationException(
                "Tenant context is already resolved for this scope and cannot be reassigned.");
        }
    }
}
