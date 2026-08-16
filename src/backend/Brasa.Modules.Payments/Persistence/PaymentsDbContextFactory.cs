using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brasa.Modules.Payments.Persistence;

/// <summary>
/// Builds a <see cref="PaymentsDbContext"/> for <c>dotnet ef</c> design-time
/// operations, independent of <c>Brasa.Api</c>'s <c>Program.cs</c>.
/// </summary>
public sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    /// <inheritdoc/>
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        // The migration (owner) connection, not the runtime brasa_app role —
        // generating and applying migrations needs DDL and RLS-policy
        // privileges the runtime role deliberately does not have. See
        // infra/initdb/01-app-role.sql.
        var connectionString = Environment.GetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=brasa;Username=brasa;Password=devonly";

        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "payments"))
            .Options;

        return new PaymentsDbContext(options, new TenantContext(), new TenantContextAccessor(), new SystemClock());
    }
}
