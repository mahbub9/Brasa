using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brasa.Modules.Catalog.Persistence;

/// <summary>
/// Builds a <see cref="CatalogDbContext"/> for <c>dotnet ef</c> design-time
/// operations, independent of <c>Brasa.Api</c>'s <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// Without this, <c>dotnet ef migrations add</c> falls back to booting the
/// startup project's actual host to obtain a configured context. That works
/// today, but ties migration generation to whatever Program.cs's startup path
/// grows to depend on — seeding, auth, external calls. This factory keeps
/// migration generation fast and independent of all of it.
/// </remarks>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    /// <inheritdoc/>
    public CatalogDbContext CreateDbContext(string[] args)
    {
        // The migration (owner) connection, not the runtime brasa_app role —
        // generating and applying migrations needs DDL and RLS-policy
        // privileges the runtime role deliberately does not have. See
        // infra/initdb/01-app-role.sql.
        var connectionString = Environment.GetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=brasa;Username=brasa;Password=devonly";

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog"))
            .Options;

        // Query filters reference these only as expression-tree captures during
        // model building; migrations never evaluate them, so unresolved
        // instances are safe here.
        return new CatalogDbContext(options, new TenantContext(), new TenantContextAccessor(), new SystemClock());
    }
}
