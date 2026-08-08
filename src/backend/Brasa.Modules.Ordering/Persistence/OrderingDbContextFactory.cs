using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brasa.Modules.Ordering.Persistence;

/// <summary>
/// Builds an <see cref="OrderingDbContext"/> for <c>dotnet ef</c> design-time
/// operations. See <c>Brasa.Modules.Catalog.Persistence.CatalogDbContextFactory</c>
/// for why this exists independently of <c>Brasa.Api</c>'s <c>Program.cs</c>.
/// </summary>
public sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    /// <inheritdoc/>
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=brasa;Username=brasa;Password=devonly";

        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "ordering"))
            .Options;

        return new OrderingDbContext(options, new TenantContext(), new TenantContextAccessor(), new SystemClock());
    }
}
