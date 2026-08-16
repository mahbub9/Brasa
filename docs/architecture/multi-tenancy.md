# Multi-tenancy

> **Status:** implemented and verified live for Catalog and Ordering — not yet
> extended to the modules that don't exist yet (Identity, Payments, Reporting).
> "Verified live" means what it says: a direct query as the runtime role,
> tenant unset or set to the wrong tenant, returns zero rows; set to the owning
> tenant, all rows. See [../product/status.md](../product/status.md) and
> [ADR 0010](decisions/0010-rls-runtime-role-split.md) for a finding worth
> reading before assuming this page describes something that just works: the
> first implementation was fully policy-correct and completely inert, because
> the connecting role was an undetected superuser.

## Hierarchy

```
Organization (tenant)        the customer; the isolation boundary
   └── Site                  a restaurant location
        └── Terminal         a POS device, or the Site Agent itself
```

`TenantId` is the organization. **Data never crosses it.**

## Two layers of enforcement

Isolation is enforced twice, on purpose.

### 1. EF Core global query filter — convenience

Applied to every entity implementing `ITenantOwned`. Keeps `WHERE tenant_id = @x`
out of every query.

It is **not** a security boundary. It is bypassed by raw SQL, by Dapper, by
reporting views, and by a single forgotten `IgnoreQueryFilters()`.

An entity that also implements `ISoftDeletable` (e.g. `MenuItem`, CAT-18) gets
`AND deleted_at_utc IS NULL` added to the same filter automatically —
`ModelBuilderExtensions.ApplyTenantQueryFilters` combines both predicates for
any entity that needs them, so a soft-deleted row disappears from ordinary
queries the same way a wrong-tenant row does, without every module having to
remember to add its own `Where(x => x.DeletedAtUtc == null)`. An admin view
that genuinely needs deleted rows calls `IgnoreQueryFilters()` explicitly, so
that intent is visible at the call site. `TenantAwareDbContext` separately
refuses to let a soft-deletable entity reach `EntityState.Deleted` at all —
calling `DbSet.Remove()` on one throws, forcing the real deletion path through
the entity's own domain method (e.g. `MenuItem.Delete(now)`).

### 2. PostgreSQL row-level security — the real defence

An RLS policy on every tenant-owned table, keyed off a session variable set at
the start of each request from `ITenantContext.TenantId`.

The database refuses to return other tenants' rows **regardless of how they were
asked for**. This is why ADR [0005](decisions/0005-plain-guid-ids.md) chose plain
`Guid` ids over strongly-typed ones: compile-time typing protects only code paths
that go through the type system, while RLS protects all of them.

> ⚠️ **That guarantee has one precondition: the connecting role must not be a
> superuser.** PostgreSQL superusers bypass RLS unconditionally — `FORCE ROW
> LEVEL SECURITY` does not change this, it only affects the table *owner*. The
> application connects as `brasa_app`, an ordinary role created by
> `infra/initdb/01-app-role.sql` specifically because the default bootstrap
> role is a superuser. See [ADR 0010](decisions/0010-rls-runtime-role-split.md).

## Tenant context

[`ITenantContext`](https://github.com/mahbub9/Brasa/blob/main/src/backend/Brasa.Shared/Tenancy/ITenantContext.cs)
is registered **scoped** and populated once per request by the tenant-resolution
middleware, from the caller's token.

`TenantContext.Resolve(...)` may be called **exactly once** per scope. A second
call throws. A bug that tries to switch tenants mid-request fails loudly instead
of silently serving one customer's data to another.

## The system context

Some work legitimately crosses tenants — the monthly SAF-T submission sweep, for
example. `TenantContext.ResolveAsSystem()` marks the scope as privileged; no
tenant is pushed to the query-filter accessor, so system code is expected to
query across tenants explicitly rather than rely on an implicit single-tenant
filter.

**The privileged connection path (DAT-07)**: exactly the "third, narrowly-scoped
role" this section once called the likely answer, not the tempting-but-wrong
shortcut of connecting as a superuser or a `BYPASSRLS` role — see
[ADR 0010](decisions/0010-rls-runtime-role-split.md) for why that shortcut is a
mistake this codebase already made once and does not intend to make again.
`brasa_system` (`infra/initdb/02-system-role.sql`) is an ordinary role: no
`SUPERUSER`, no `BYPASSRLS`, and read-only by construction — no table's
migration ever grants it `INSERT`/`UPDATE`/`DELETE`. Every tenant-owned table
gets a second, role-scoped RLS policy via `RowLevelSecurity.EnableSystemReadFor`
(`USING (true)`, but only for connections authenticated as `brasa_system` —
Postgres never evaluates a policy for a role it doesn't name, so this can never
widen what an ordinary `brasa_app` request sees). `ModulePersistenceExtensions`
picks a physically separate connection string/pool (`ConnectionStrings:PostgresSystem`)
whenever a scope's `ITenantContext.IsSystemContext` is set, so a tenant-scoped
request can never end up running on a connection that still carries
`brasa_system`'s elevated privileges — a structural guarantee, not one that
depends on connection-pool reset-on-close behaving correctly.

> **A system-context query must call `IgnoreQueryFilters()` explicitly to
> actually see more than one tenant.** The RLS policy admitting every row is
> only half the picture — EF Core's own global query filter
> (`ApplyTenantQueryFilters`) always compiles to `TenantId == accessor.CurrentTenantId`,
> and a system-context scope never pushes a tenant into that accessor
> (`CurrentTenantId` stays `Guid.Empty`, which matches no real tenant). Without
> `IgnoreQueryFilters()`, a system-context `DbContext` queries a real,
> unrestricted-by-RLS connection and still gets zero rows back — this is the
> "expected to query across tenants explicitly, not rely on an implicit
> single-tenant filter" sentence above, made concrete. Verified live in
> `SystemContextIntegrationTests.The_real_ResolveAsSystem_path_through_EF_sees_every_tenant_once_the_query_filter_is_lifted`.

> **`ResolveAsSystem()` must never be reachable from an HTTP request path.** It is
> for background jobs and migrations only.

> **Not meaningfully applicable**: the `InMemory` provider (ADR 0012, the beta
> pilot's SQLite path) has no RLS and no `brasa_system` role — every module's
> `DbContext` there shares one connection regardless of `IsSystemContext`, so
> `IgnoreQueryFilters()` alone already reveals everything in that one store, the
> same as it would for any tenant. There is no separate cross-tenant boundary to
> test there, because the beta pilot is single-tenant by deployment design in
> the first place — the same accepted trade-down `InMemory` already makes for
> the ordinary RLS boundary.

## Entities

Opt a table in by implementing `ITenantOwned`. `Entity` already does.

`Entity.AssignTenant()` is called by the DbContext on insert and refuses to
reassign an entity that already belongs to a different tenant.

A tenant-scoped table that forgets `ITenantOwned` is a data leak, so
`TenantIsolationTests` asserts by reflection that every entity in every module
implements it or appears on an explicit allow-list of shared reference data
(VAT rate tables, country data).

## Scaling path

One PostgreSQL database, `tenant_id` on every row. This comfortably serves
thousands of tenants.

If a single database becomes the limit, `tenant_id` is already the shard key —
routing happens at connection selection, and no schema change is needed. That is
the whole reason it is present from the first migration rather than added later.
