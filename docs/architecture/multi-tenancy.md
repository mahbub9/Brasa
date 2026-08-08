# Multi-tenancy

> **Status:** contracts exist (`ITenantContext`, `ITenantOwned`). The EF Core
> layer and RLS policies are **not yet implemented** — see
> [../product/status.md](../product/status.md).

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

### 2. PostgreSQL row-level security — the real defence

An RLS policy on every tenant-owned table, keyed off a session variable set at
the start of each request from `ITenantContext.TenantId`.

The database refuses to return other tenants' rows **regardless of how they were
asked for**. This is why ADR [0005](decisions/0005-plain-guid-ids.md) chose plain
`Guid` ids over strongly-typed ones: compile-time typing protects only code paths
that go through the type system, while RLS protects all of them.

## Tenant context

[`ITenantContext`](../../src/backend/Brasa.Shared/Tenancy/ITenantContext.cs)
is registered **scoped** and populated once per request by the tenant-resolution
middleware, from the caller's token.

`TenantContext.Resolve(...)` may be called **exactly once** per scope. A second
call throws. A bug that tries to switch tenants mid-request fails loudly instead
of silently serving one customer's data to another.

## The system context

Some work legitimately crosses tenants — the monthly SAF-T submission sweep, for
example. `TenantContext.ResolveAsSystem()` marks the scope as privileged and
connects using a database role that bypasses RLS.

> **`ResolveAsSystem()` must never be reachable from an HTTP request path.** It is
> for background jobs and migrations only.

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
