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
/// Call <see cref="EnableFor"/> <b>and</b> <see cref="EnableSystemReadFor"/> for
/// every tenant-owned table in the same migration that creates it. A table
/// created without the first policy is a data leak waiting for its first bug; a
/// table created without the second is invisible to background jobs and
/// migrations forever, a silent gap discovered only when one actually needs it.
/// Deliberately two separate calls, not one that does both — <see cref="EnableFor"/>
/// is itself called from every historical migration already committed, and
/// folding <see cref="EnableSystemReadFor"/> into it would silently change what
/// those old migrations do the next time they run against a fresh database
/// (found exactly this way: a fresh Testcontainers run failed with "policy
/// already exists" the moment this was tried, because the old migrations and a
/// new dedicated one both tried to create it).
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
    /// The privileged, read-only, cross-tenant role background jobs and
    /// migrations connect as (DAT-07) — see
    /// <c>infra/initdb/02-system-role.sql</c> and
    /// <c>docs/architecture/multi-tenancy.md</c>'s "system context" section.
    /// Deliberately not <c>BYPASSRLS</c> or superuser, the exact mistake
    /// <see cref="AppRole"/> itself was created to avoid (see
    /// <a href="../../architecture/decisions/0010-rls-runtime-role-split.md">ADR 0010</a>)
    /// — it gets its own explicit, role-scoped policy via
    /// <see cref="EnableSystemReadFor"/> instead.
    /// </summary>
    public const string SystemRole = "brasa_system";

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

    /// <summary>
    /// Grants <see cref="SystemRole"/> read-only access across every tenant on
    /// one table (DAT-07). Call this alongside <see cref="EnableFor"/> for
    /// every new tenant-owned table's own creating migration — see that
    /// method's own class-level remarks for why this is a second, deliberate
    /// call rather than folded into it. A table that enabled RLS before this
    /// existed gets its own follow-up migration calling this directly — see
    /// e.g. each module's own <c>AddSystemContextRole</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PostgreSQL evaluates multiple permissive RLS policies on the same table
    /// with OR — but only among policies that apply to the connecting role in
    /// the first place. A policy created <c>TO "brasa_system"</c> is invisible
    /// to <see cref="AppRole"/> connections and vice versa, so this can never
    /// widen what an ordinary tenant-scoped request sees; it only widens what
    /// a connection authenticated as <see cref="SystemRole"/> sees, which
    /// nothing in this codebase can become except a background job or a
    /// migration (see <c>ITenantContext.ResolveAsSystem</c> — "must never be
    /// called from a request path").
    /// </para>
    /// <para>
    /// Read-only is enforced twice, redundantly on purpose: the policy itself
    /// is <c>FOR SELECT</c> only, and the <c>GRANT</c> below never includes
    /// <c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c> — an attempted write is
    /// rejected by the privilege check before RLS is even consulted, so a bug
    /// in the policy expression alone could never turn this into a write path.
    /// </para>
    /// </remarks>
    public static void EnableSystemReadFor(this MigrationBuilder migrationBuilder, string table, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var qualifiedSchema = $"\"{schema}\"";
        var qualifiedTable = $"{qualifiedSchema}.\"{table}\"";
        var policy = $"{table}_system_read";
        var role = $"\"{SystemRole}\"";

        migrationBuilder.Sql($"""
            CREATE POLICY "{policy}" ON {qualifiedTable}
                FOR SELECT
                TO {role}
                USING (true);

            GRANT USAGE ON SCHEMA {qualifiedSchema} TO {role};
            GRANT SELECT ON {qualifiedTable} TO {role};
            """);
    }

    /// <summary>
    /// Revokes access, removes the policy, and disables row-level security. Used
    /// in <c>Down</c>. Call <see cref="DisableSystemReadFor"/> alongside this too
    /// if the table's own creating migration called <see cref="EnableSystemReadFor"/>
    /// — the two stay as deliberately separate calls symmetrically with their
    /// <c>Enable*</c> counterparts, see <see cref="EnableFor"/>'s own remarks.
    /// </summary>
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

    /// <summary>The <see cref="EnableSystemReadFor"/> half of <see cref="DisableFor"/> — used in <c>Down</c>.</summary>
    public static void DisableSystemReadFor(this MigrationBuilder migrationBuilder, string table, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var qualifiedTable = $"\"{schema}\".\"{table}\"";
        var role = $"\"{SystemRole}\"";

        migrationBuilder.Sql($"""
            REVOKE SELECT ON {qualifiedTable} FROM {role};
            DROP POLICY IF EXISTS "{table}_system_read" ON {qualifiedTable};
            """);
    }
}
