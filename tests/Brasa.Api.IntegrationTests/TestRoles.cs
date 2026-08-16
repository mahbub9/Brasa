using Npgsql;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// Creates <c>brasa_app</c> and <c>brasa_system</c> on a fresh Testcontainers
/// Postgres, mirroring <c>infra/initdb/01-app-role.sql</c> and
/// <c>02-system-role.sql</c> — <c>docker-entrypoint-initdb.d</c> scripts never
/// run against a Testcontainers image, so every integration test that migrates
/// a real module schema needs both roles created by hand first.
/// </summary>
/// <remarks>
/// A single shared helper, not one copy per test class, on purpose:
/// <see cref="OrderConcurrencyIntegrationTests"/> and
/// <see cref="TenantIsolationIntegrationTests"/> both used to hand-roll just
/// the <c>brasa_app</c> half — fine on its own, until a module's migrations
/// started granting to <c>brasa_system</c> too (DAT-07) and every class that
/// hadn't been updated started failing with "role brasa_system does not
/// exist." One helper both roles now flow through means a fourth test class
/// can never independently drift out of sync with what the real migrations
/// actually need.
/// </remarks>
internal static class TestRoles
{
    public static async Task EnsureAppAndSystemRolesExistAsync(string superuserConnectionString)
    {
        await using var connection = new NpgsqlConnection(superuserConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'brasa_app') THEN
                    CREATE ROLE brasa_app WITH LOGIN PASSWORD 'devonly_app';
                END IF;
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'brasa_system') THEN
                    CREATE ROLE brasa_system WITH LOGIN PASSWORD 'devonly_system';
                END IF;
            END
            $$;
            GRANT CONNECT ON DATABASE brasa TO brasa_app;
            GRANT CONNECT ON DATABASE brasa TO brasa_system;
            """;
        await command.ExecuteNonQueryAsync();
    }
}
