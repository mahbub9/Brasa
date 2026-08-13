using Brasa.Modules.Identity.Domain;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Modules.Identity.Persistence;

/// <summary>
/// Owns the <c>identity</c> schema: organizations, sites, terminals (IDN-01)
/// and staff PIN accounts (IDN-08/09). Roles-and-permissions beyond a bare
/// Staff/Manager tag, OAuth/JWT sessions and terminal pairing — the rest of
/// this module's own epic — are separate, not-yet-built rows.
/// </summary>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantContextAccessor,
    IClock clock)
    : TenantAwareDbContext(options, tenantContext, tenantContextAccessor, clock)
{
    /// <inheritdoc/>
    protected override string Schema => "identity";

    /// <summary>The restaurant business, top of the Organization → Site → Terminal hierarchy.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>Physical restaurant locations belonging to an organization.</summary>
    public DbSet<Site> Sites => Set<Site>();

    /// <summary>Physical POS devices registered at a site.</summary>
    public DbSet<Terminal> Terminals => Set<Terminal>();

    /// <summary>Staff members who can sign in with a PIN (IDN-08/09), scoped to a site.</summary>
    public DbSet<Staff> Staff => Set<Staff>();

    /// <summary>Per-tenant, optionally per-platform on/off switches (IDN-16).</summary>
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
