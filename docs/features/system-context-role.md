# Privileged system-context role for background jobs

> **Status:** ✅ done — the role and its RLS policy, not a real consumer yet.
> **Module:** Shared kernel (`Brasa.Shared.Persistence`/`Brasa.Shared.Tenancy`) — every module's tables opt in
> **Roadmap:** I3 (`DAT-07`, pulled forward)

## What it is

Some work legitimately crosses tenants — the monthly SAF-T submission sweep
is the example [multi-tenancy.md](../architecture/multi-tenancy.md) itself
names. `ITenantContext.ResolveAsSystem()` had existed since day one as a bare
flag with no real effect: no tenant pushed to the query filter, but the
connection still authenticated as `brasa_app`, so the RLS policy's own
"unset tenant matches no rows" default meant a system-context query saw
*nothing*, not everything. `brasa_system` (`infra/initdb/02-system-role.sql`)
is the real, privileged, read-only, cross-tenant database role that makes
`ResolveAsSystem()` actually do what its own doc comment always claimed.

## Why it works this way

**A second role, not a superuser or `BYPASSRLS`.** [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md)
already documents the mistake once made for the ordinary runtime path: the
bootstrap Postgres role is a superuser, and superusers bypass row-level
security unconditionally, `FORCE ROW LEVEL SECURITY` notwithstanding.
`brasa_system` is an ordinary role — no `SUPERUSER`, no `BYPASSRLS` — scoped
entirely by its own explicit RLS policy instead of by bypassing the security
system altogether.

**Read-only, enforced twice, redundantly on purpose.** `RowLevelSecurity.EnableSystemReadFor`
creates a `FOR SELECT ... USING (true)` policy *and* a `GRANT` that never
includes `INSERT`/`UPDATE`/`DELETE`. A bug in the policy expression alone
could never turn this into a write path — the privilege check rejects a
write before RLS is even consulted.

**Scoped to the role by name, so it can never widen the ordinary path.**
Postgres evaluates multiple permissive RLS policies on the same table with
OR — but only among policies that name the connecting role. A policy
created `TO "brasa_system"` is invisible to `brasa_app` connections; adding
it cannot change what an ordinary tenant-scoped request sees, proven
directly (not just argued) in `SystemContextIntegrationTests.Brasa_app_still_sees_nothing_across_tenants_regardless_of_brasa_systems_existence`.

**A physically separate connection pool, not a role switch on the same
one.** `ModulePersistenceExtensions` picks `ConnectionStrings:PostgresSystem`
instead of `Postgres` whenever a scope's `ITenantContext.IsSystemContext` is
set, resolved from the scoped `IServiceProvider` at `DbContext`-construction
time. Different Npgsql credentials mean a genuinely separate connection
pool (Npgsql pools per exact connection string) — an ordinary tenant-scoped
request can never end up running on a connection that still carries
`brasa_system`'s privileges, a structural guarantee rather than one resting
on connection-pool reset-on-close behaving correctly. A `SET ROLE`-based
design was considered and rejected for exactly this reason: it would have
worked, but only by trusting Npgsql's own reset-on-close semantics for a
security boundary, when a separate pool sidesteps the question entirely.

**Two separate `EnableFor` calls, not one method doing both.** The first
implementation attempt folded `EnableSystemReadFor` directly into the
existing `EnableFor` so every RLS-enabling table got both policies from one
call site. It broke immediately: `EnableFor`'s C# implementation is shared,
not snapshotted per historical migration, so the change silently altered
what every *already-committed* migration does the next time it runs against
a fresh database — a brand-new Testcontainers run failed with `policy
"..._system_read" already exists`, because the old migrations (now calling
the new `EnableFor`) and a new dedicated migration both tried to create it.
Fixed by keeping the calls separate and adding one `AddSystemContextRole`
migration per module, granting the 19 pre-existing tables explicitly.

## Behaviour

1. Background-job or migration code calls `tenantContext.ResolveAsSystem()`
   at the start of its own scope, before touching any `DbContext` — the same
   "resolve once, before any data access" shape `Resolve(tenantId)` already
   requires for an ordinary request.
2. The next time that scope constructs a module's `DbContext`,
   `ModulePersistenceExtensions` sees `IsSystemContext == true` and connects
   via `ConnectionStrings:PostgresSystem` (`brasa_system`) instead of
   `Postgres` (`brasa_app`).
3. `TenantSessionInterceptor` never sets `brasa.tenant_id` for this
   connection — `HasTenant` is `false` since `TenantId` stayed
   `Guid.Empty` — so the RLS policy scoped to `brasa_system`
   (`USING (true)`) is the only one in play; the ordinary tenant-scoped
   policy on the same table is simply never evaluated for this role.
4. **The caller must still call `IgnoreQueryFilters()` explicitly.** The RLS
   policy admitting every row is only half the picture — EF Core's own
   global query filter (`ApplyTenantQueryFilters`) always compiles to
   `TenantId == accessor.CurrentTenantId`, and a system context never pushes
   a tenant into that accessor, so `CurrentTenantId` stays `Guid.Empty` —
   which matches no real tenant. Without `IgnoreQueryFilters()`, a
   system-context `DbContext` queries a real, unrestricted-by-RLS connection
   and still gets zero rows back.

## Offline behaviour

Not applicable — this is a cloud-API/background-job concern; there is no
Site Agent or offline path that would ever call `ResolveAsSystem()`.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| A system-context connection attempts `INSERT`/`UPDATE`/`DELETE` | Rejected by the table `GRANT` itself, before RLS is even consulted | `PostgresException`, `SqlState 42501` (`insufficient_privilege`) |
| A system-context connection attempts DDL (e.g. `DROP TABLE`) | Rejected — `brasa_system` is not a superuser | `PostgresException`, `SqlState 42501` |
| A system-context query forgets `IgnoreQueryFilters()` | The EF query filter alone still applies `TenantId == Guid.Empty`, matching no real tenant | An empty result set, not an error — the likeliest way this gets misused, since nothing throws |
| `ResolveAsSystem()` is (incorrectly) reachable from an HTTP request path | Not guarded in code today — enforced by convention only ("must never be called from a request path," `ITenantContext.IsSystemContext`'s own doc comment) | No current call site does this; would be a genuine cross-tenant data exposure if one ever did |

## Data

No new persisted column or schema. Every tenant-owned table across all four
modules (Catalog, Ordering, Floor, Identity — 19 tables) gets a second,
role-scoped RLS policy (`{table}_system_read`) via `RowLevelSecurity.EnableSystemReadFor`,
either from a table's own creating migration going forward, or a one-time
`AddSystemContextRole` migration per module for tables that predate it.

## API

No new routes — this is a persistence/DI-layer mechanism with no HTTP
surface of its own. `ConnectionStrings:PostgresSystem` is a new configuration
key (`appsettings.json`), read only when `Database:Provider` is `Postgres`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None directly. The eventual SAF-T submission sweep (`FIS-23`, unbuilt) is
the concrete example this mechanism exists for, but nothing fiscal reads
through it yet.

## Permissions

Not a user-facing permission — `ResolveAsSystem()` is code-level, called by
trusted background-job/migration code only, never derived from a staff
role or a request. There is no `Staff`/`StaffRole` check anywhere in this
path.

## Testing

`SystemContextIntegrationTests.cs` — against a real, disposable
Testcontainers Postgres:

- `brasa_system` sees rows across multiple tenants with zero session
  variable set at all.
- `brasa_app` stays exactly as isolated as before `brasa_system` existed —
  adding the new role's policy provably cannot widen the old one's.
- A write (`INSERT`) and a DDL statement (`DROP TABLE`) are both rejected
  with `insufficient_privilege`.
- The real `ResolveAsSystem()` → `ModulePersistenceExtensions` → EF path,
  end to end: the query filter alone still shows nothing; `IgnoreQueryFilters()`
  shows every tenant.

Also see `MigrationsEnvVarCollection.cs` and `TestRoles.cs` — the shared
xUnit collection and role-creation helper this test class needed, after
adding it as a third consumer of an existing `BRASA_MIGRATIONS_CONNECTION`
env-var pattern surfaced a real race between test classes (see
[order-concurrency.md](order-concurrency.md)'s own testing section for
where that pattern was first found).

## Open questions

- **No real consumer yet.** The SAF-T submission sweep (`FIS-23`) — the
  concrete example this mechanism exists for — doesn't exist. This ships
  the mechanism only, the same "mechanism before the trigger" shape this
  codebase uses everywhere else (`TaxRule.Resolve` not wired into
  `AddLine`, price lists not resolved through `AddLine`, etc.) — first
  proven correct in isolation, then wired into a real caller once one
  exists.
- **Not meaningfully applicable to the `InMemory` beta-pilot provider**
  (ADR 0012) — every module's `DbContext` there shares one SQLite
  connection regardless of `IsSystemContext`, so there's no separate
  cross-tenant boundary to test in the first place; the beta pilot is
  single-tenant by deployment design.
- **`ResolveAsSystem()`'s "never from a request path" rule is enforced by
  convention, not code.** Nothing today throws if an HTTP endpoint handler
  called it. Worth a real guard (e.g. asserting no `HttpContext` is
  ambient) if a future change ever makes that mistake easy to make by
  accident.
