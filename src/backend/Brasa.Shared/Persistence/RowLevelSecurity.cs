using Microsoft.EntityFrameworkCore.Migrations;

namespace Brasa.Shared.Persistence;

/// <summary>
/// Emits the PostgreSQL row-level security policies that are the **real** tenant
/// isolation boundary.
/// </summary>
/// <remarks>
/// <para>
/// EF Core's global query filter is a convenience that raw SQL, Dapper, a
/// reporting view or a forgotten <c>IgnoreQueryFilters()</c> all bypass. An RLS
/// policy is enforced by the database regardless of how the rows were asked for.
/// </para>
/// <para>
/// Call <see cref="EnableFor"/> for every tenant-owned table in the same
/// migration that creates it. A table that is created without a policy is a data
/// leak waiting for its first bug.
/// </para>
/// <para>See <c>docs/architecture/multi-tenancy.md</c>.</para>
/// </remarks>
public static class RowLevelSecurity
{
    /// <summary>
    /// The PostgreSQL session setting holding the current tenant. Set per
    /// connection by <see cref="TenantSessionInterceptor"/>.
    /// </summary>
    public const string TenantSetting = "brasa.tenant_id";

    /// <summary>
    /// The role the application connects as at runtime. Deliberately not the
    /// migration role — see <c>infra/initdb/01-app-role.sql</c> for why a
    /// superuser makes every RLS policy a no-op regardless of how the policy
    /// itself is written.
    /// </summary>
    public const string AppRole = "brasa_app";

    /// <summary>
    /// Enables and forces row-level security on a table, with a policy admitting
    /// only rows belonging to the tenant in the current session.
    /// </summary>
    /// <param name="migrationBuilder">The migration being built.</param>
    /// <param name="table">Table name.</param>
    /// <param name="schema">Owning module's schema.</param>
    /// <param name="tenantIdColumn">
    /// The tenant column's actual database name. Defaults to <c>"TenantId"</c> —
    /// EF Core's default convention preserves the C# property name verbatim and
    /// quoted, so it is case-sensitive and must match exactly. Pass the real
    /// name if the owning entity configuration overrides it with
    /// <c>HasColumnName</c>.
    /// </param>
    /// <param name="appRole">
    /// The runtime role to grant table access to. See <see cref="AppRole"/> —
    /// this must never be the migration/owner role, or the policy created here
    /// has no effect on the connection that actually serves requests.
    /// </param>
    public static void EnableFor(
        this MigrationBuilder migrationBuilder,
        string table,
        string schema,
        string tenantIdColumn = "TenantId",
        string appRole = AppRole)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var qualifiedSchema = $"\"{schema}\"";
        var qualifiedTable = $"{qualifiedSchema}.\"{table}\"";
        var policy = $"{table}_tenant_isolation";
        var column = $"\"{tenantIdColumn}\"";
        var role = $"\"{appRole}\"";

        migrationBuilder.Sql($"""
            ALTER TABLE {qualifiedTable} ENABLE ROW LEVEL SECURITY;

            -- FORCE matters for the table OWNER: without it, the owner (the
            -- migration role) bypasses every policy. It does NOT affect
            -- superusers, which always bypass RLS regardless — this is why the
            -- application must run as a separate, non-superuser role. See
            -- infra/initdb/01-app-role.sql.
            ALTER TABLE {qualifiedTable} FORCE ROW LEVEL SECURITY;

            -- current_setting(..., true) returns NULL when the setting is absent,
            -- and NULL = uuid is NULL, so an unset tenant matches no rows.
            -- The default is to see nothing, which is the only safe default.
            CREATE POLICY "{policy}" ON {qualifiedTable}
                USING ({column} = NULLIF(current_setting('{TenantSetting}', true), '')::uuid)
                WITH CHECK ({column} = NULLIF(current_setting('{TenantSetting}', true), '')::uuid);

            -- The policy is inert until the runtime role can reach the table at
            -- all. Granted here, in the same migration, so a new table is never
            -- briefly RLS-enabled-but-unreachable or reachable-but-unpolicied.
            GRANT USAGE ON SCHEMA {qualifiedSchema} TO {role};
            GRANT SELECT, INSERT, UPDATE, DELETE ON {qualifiedTable} TO {role};
            """);
    }

    /// <summary>Revokes access, removes the policy, and disables row-level security. Used in <c>Down</c>.</summary>
    public static void DisableFor(
        this MigrationBuilder migrationBuilder,
        string table,
        string schema,
        string appRole = AppRole)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var qualifiedTable = $"\"{schema}\".\"{table}\"";
        var role = $"\"{appRole}\"";

        migrationBuilder.Sql($"""
            REVOKE SELECT, INSERT, UPDATE, DELETE ON {qualifiedTable} FROM {role};
            DROP POLICY IF EXISTS "{table}_tenant_isolation" ON {qualifiedTable};
            ALTER TABLE {qualifiedTable} NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {qualifiedTable} DISABLE ROW LEVEL SECURITY;
            """);
    }
}
