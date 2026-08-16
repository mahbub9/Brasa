using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// DAT-07 — proves <c>brasa_system</c> actually works as the privileged,
/// read-only, cross-tenant role <c>docs/architecture/multi-tenancy.md</c>'s
/// own "system context" section calls for, against a real, disposable
/// PostgreSQL container. Mirrors <see cref="TenantIsolationIntegrationTests"/>'s
/// own "don't just assert it, run it" discipline for the ordinary role.
/// </summary>
[Collection("Migrations env var")]
public sealed class SystemContextIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("brasa")
        .WithUsername("brasa")
        .WithPassword("devonly")
        .Build();

    private string _appConnectionString = string.Empty;
    private string _systemConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var superuserConnectionString = _postgres.GetConnectionString();

        // Mirrors infra/initdb/01-app-role.sql and 02-system-role.sql — see
        // TestRoles's own remarks for why this is a shared helper.
        await TestRoles.EnsureAppAndSystemRolesExistAsync(superuserConnectionString);

        // Built via CatalogDbContextFactory — see TenantIsolationIntegrationTests's
        // own remarks on why this, not a hand-rolled DbContextOptionsBuilder.
        Environment.SetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION", superuserConnectionString);
        try
        {
            await using var db = new CatalogDbContextFactory().CreateDbContext([]);
            await db.Database.MigrateAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION", null);
        }

        var appBuilder = new NpgsqlConnectionStringBuilder(superuserConnectionString)
        {
            Username = "brasa_app",
            Password = "devonly_app",
        };
        _appConnectionString = appBuilder.ConnectionString;

        var systemBuilder = new NpgsqlConnectionStringBuilder(superuserConnectionString)
        {
            Username = "brasa_system",
            Password = "devonly_system",
        };
        _systemConnectionString = systemBuilder.ConnectionString;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Brasa_system_sees_every_tenant_with_no_session_variable_set_at_all()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedCategoryAsSuperuserAsync(tenantA, "Tenant A category");
        await SeedCategoryAsSuperuserAsync(tenantB, "Tenant B category");

        // Deliberately no set_config('brasa.tenant_id', ...) call at all —
        // brasa_system's own policy (RowLevelSecurity.EnableSystemReadFor)
        // is USING (true), it never inspects the session variable the
        // ordinary brasa_app policy reads.
        await using var connection = new NpgsqlConnection(_systemConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Name\" FROM catalog.menu_categories ORDER BY \"Name\";";
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        names.ShouldBe(["Tenant A category", "Tenant B category"]);
    }

    [Fact]
    public async Task Brasa_app_still_sees_nothing_across_tenants_regardless_of_brasa_systems_existence()
    {
        // The system role's own policy is scoped TO "brasa_system" —
        // PostgreSQL only evaluates a policy for the role it names, so
        // adding it can never widen what an ordinary brasa_app connection
        // sees. Proven directly, not just argued: same seeded rows as
        // above, queried as brasa_app with no tenant set.
        var tenantA = Guid.CreateVersion7();
        await SeedCategoryAsSuperuserAsync(tenantA, "Still isolated");

        await using var connection = new NpgsqlConnection(_appConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM catalog.menu_categories;";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        count.ShouldBe(0, "no tenant session variable set, brasa_app's own policy admits nothing");
    }

    [Fact]
    public async Task Brasa_system_cannot_write_a_single_row()
    {
        await using var connection = new NpgsqlConnection(_systemConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO catalog.menu_categories
                ("Id", "Name", "DisplayOrder", "IsVisible", "TenantId", "CreatedAtUtc")
            VALUES (gen_random_uuid(), 'Should never land', 1, true, gen_random_uuid(), now());
            """;

        // Rejected by the GRANT itself (SELECT only), before RLS's own WITH
        // CHECK is even consulted -- see EnableSystemReadFor's own remarks
        // on why this is enforced twice, redundantly, on purpose.
        var exception = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        exception.SqlState.ShouldBe("42501"); // insufficient_privilege
    }

    [Fact]
    public async Task Brasa_system_cannot_run_ddl_either()
    {
        await using var connection = new NpgsqlConnection(_systemConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE catalog.menu_categories;";

        var exception = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        exception.SqlState.ShouldBe("42501"); // insufficient_privilege -- not a superuser, same as brasa_app
    }

    [Fact]
    public async Task The_real_ResolveAsSystem_path_through_EF_sees_every_tenant_once_the_query_filter_is_lifted()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedCategoryAsSuperuserAsync(tenantA, "EF path tenant A");
        await SeedCategoryAsSuperuserAsync(tenantB, "EF path tenant B");

        var tenantContext = new TenantContext();
        tenantContext.ResolveAsSystem();

        // The exact construction ModulePersistenceExtensions.AddModuleDbContext
        // performs when ITenantContext.IsSystemContext is set: the
        // brasa_system connection string, not brasa_app's.
        await using var db = new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(_systemConnectionString).Options,
            tenantContext,
            new TenantContextAccessor(),
            new SystemClock());

        // The EF query filter itself (ApplyTenantQueryFilters) always
        // compiles to `TenantId == accessor.CurrentTenantId` -- for a
        // system context that's never Guid.Empty vs Guid.Empty, which
        // matches no real tenant. This is the "system code is expected to
        // query across tenants explicitly, not rely on an implicit
        // single-tenant filter" multi-tenancy.md itself names: the RLS
        // policy at the database level already admits every row: without
        // IgnoreQueryFilters(), EF's own convenience filter would still
        // narrow that back down to nothing, so a system-context caller must
        // opt out of it explicitly.
        var filtered = await db.Categories.ToListAsync();
        filtered.ShouldBeEmpty("the EF query filter alone still applies Guid.Empty, matching no real tenant");

        var unfiltered = await db.Categories.IgnoreQueryFilters().Select(c => c.Name).OrderBy(n => n).ToListAsync();
        unfiltered.ShouldBe(["EF path tenant A", "EF path tenant B"]);
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
}
