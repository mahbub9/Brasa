using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brasa.Modules.Identity.Persistence;

/// <summary>
/// Builds an <see cref="IdentityDbContext"/> for <c>dotnet ef</c> design-time
/// operations, independent of <c>Brasa.Api</c>'s <c>Program.cs</c>.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    /// <inheritdoc/>
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // The migration (owner) connection, not the runtime brasa_app role —
        // generating and applying migrations needs DDL and RLS-policy
        // privileges the runtime role deliberately does not have. See
        // infra/initdb/01-app-role.sql.
        var connectionString = Environment.GetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=brasa;Username=brasa;Password=devonly";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"))
            .Options;

        // Query filters reference these only as expression-tree captures during
        // model building; migrations never evaluate them, so unresolved
        // instances are safe here.
        return new IdentityDbContext(options, new TenantContext(), new TenantContextAccessor(), new SystemClock());
    }
}
