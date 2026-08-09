using System.Globalization;
using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// QA-10 — proves row-level security actually isolates tenants, against a
/// real, disposable PostgreSQL container. This is the automated version of
/// the manual psql verification that caught the RLS-superuser-bypass bug
/// (<a href="../../docs/architecture/decisions/0010-rls-runtime-role-split.md">ADR 0010</a>)
/// — it exists so that bug class can never come back silently.
/// </summary>
/// <remarks>
/// Deliberately queries with raw SQL as <c>brasa_app</c>, not through
/// <c>CatalogDbContext</c>. Going through EF would also exercise
/// <c>ApplyTenantQueryFilters</c>, which is a convenience, not the security
/// boundary (see <c>docs/architecture/multi-tenancy.md</c>) — if RLS were
/// ever silently disabled again, the EF filter alone would mask exactly the
/// failure this test exists to catch.
/// </remarks>
public sealed class TenantIsolationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("brasa")
        .WithUsername("brasa")
        .WithPassword("devonly")
        .Build();

    private string _appConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var superuserConnectionString = _postgres.GetConnectionString();

        // Mirrors infra/initdb/01-app-role.sql — the unprivileged runtime
        // role RLS actually applies to. Run directly against the container
        // instead of relying on docker-entrypoint-initdb.d, so this test
        // needs nothing beyond the stock Testcontainers image.
        await using (var connection = new NpgsqlConnection(superuserConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'brasa_app') THEN
                        CREATE ROLE brasa_app WITH LOGIN PASSWORD 'devonly_app';
                    END IF;
                END
                $$;
                GRANT CONNECT ON DATABASE brasa TO brasa_app;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(superuserConnectionString).Options,
            new TenantContext(),
            new TenantContextAccessor(),
            new SystemClock()))
        {
            // The real migration, including the EnableFor RLS policy and the
            // GRANTs to brasa_app it carries — not a hand-rolled schema.
            await db.Database.MigrateAsync();
        }

        var appBuilder = new NpgsqlConnectionStringBuilder(superuserConnectionString)
        {
            Username = "brasa_app",
            Password = "devonly_app",
        };
        _appConnectionString = appBuilder.ConnectionString;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Brasa_app_sees_only_the_tenant_set_on_the_session_and_nothing_with_none_set()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        await SeedCategoryAsSuperuserAsync(tenantA, "Tenant A category");
        await SeedCategoryAsSuperuserAsync(tenantB, "Tenant B category");

        (await CountCategoriesAsAppRoleAsync(tenant: null)).ShouldBe(0, "no tenant session variable set");
        (await CountCategoriesAsAppRoleAsync(tenantA)).ShouldBe(1, "tenant A must see only its own row");
        (await CountCategoriesAsAppRoleAsync(tenantB)).ShouldBe(1, "tenant B must see only its own row");

        // The classic cross-tenant leak shape: tenant A's session variable
        // must never surface tenant B's row, or vice versa.
        var namesVisibleToTenantA = await CategoryNamesAsAppRoleAsync(tenantA);
        namesVisibleToTenantA.ShouldBe(["Tenant A category"]);
    }

    [Fact]
    public async Task Brasa_app_cannot_run_ddl_even_with_a_tenant_set()
    {
        await using var connection = new NpgsqlConnection(_appConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE catalog.menu_categories;";

        var exception = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        exception.SqlState.ShouldBe("42501"); // insufficient_privilege
    }

    private async Task SeedCategoryAsSuperuserAsync(Guid tenantId, string name)
    {
        var tenantContext = new TenantContext();
        tenantContext.Resolve(tenantId);

        await using var db = new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            tenantContext,
            new TenantContextAccessor(),
            new SystemClock());

        db.Categories.Add(new MenuCategory(name, 1));
        await db.SaveChangesAsync();
    }

    private async Task<int> CountCategoriesAsAppRoleAsync(Guid? tenant)
    {
        await using var connection = await OpenAppRoleConnectionAsync(tenant);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM catalog.menu_categories;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<List<string>> CategoryNamesAsAppRoleAsync(Guid tenant)
    {
        await using var connection = await OpenAppRoleConnectionAsync(tenant);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Name\" FROM catalog.menu_categories ORDER BY \"Name\";";
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<NpgsqlConnection> OpenAppRoleConnectionAsync(Guid? tenant)
    {
        var connection = new NpgsqlConnection(_appConnectionString);
        await connection.OpenAsync();

        if (tenant is { } tenantId)
        {
            await using var setCommand = connection.CreateCommand();
            setCommand.CommandText = "SELECT set_config('brasa.tenant_id', @tenantId, false);";
            setCommand.Parameters.AddWithValue("tenantId", tenantId.ToString());
            await setCommand.ExecuteNonQueryAsync();
        }

        return connection;
    }
}
