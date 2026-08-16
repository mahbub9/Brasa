using Brasa.Modules.Payments.Domain;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Modules.Payments.Persistence;

/// <summary>
/// Owns the <c>payments</c> schema: tenders recorded against orders.
/// </summary>
public sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantContextAccessor,
    IClock clock)
    : TenantAwareDbContext(options, tenantContext, tenantContextAccessor, clock)
{
    /// <inheritdoc/>
    protected override string Schema => "payments";

    /// <summary>Payments.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
