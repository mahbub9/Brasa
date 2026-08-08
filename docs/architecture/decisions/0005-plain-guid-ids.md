# ADR 0005 — Plain `Guid` ids; isolation enforced by RLS

**Status:** Accepted · **Date:** 2026-08-08

## Context

This system has many id-shaped things — `TenantId`, `SiteId`, `TerminalId`,
`UserId`, `OrderId`, `MenuItemId`. All are GUIDs, so the compiler cannot stop a
`SiteId` being passed where a `TenantId` is expected. In a multi-tenant fiscal
system that class of mistake is not a cosmetic bug: it is a cross-tenant data
leak.

Strongly-typed ids (a wrapper struct per id type) would catch it at compile time,
at the cost of EF Core value converters, JSON converters, and per-type
boilerplate.

## Decision

Use plain `Guid`, and enforce tenant isolation **at the database** with
PostgreSQL row-level security, backed by an EF Core global query filter.

## Rationale

Strongly-typed ids only protect code paths that go through the type system. They
do nothing for:

- raw SQL and Dapper queries
- a forgotten `IgnoreQueryFilters()`
- reporting views
- a bug in the tenant-resolution middleware itself

RLS protects **all** of those, because the database refuses to return the rows
regardless of how they were asked for. Given a choice of one mechanism, the one
that cannot be bypassed is worth more than the one that is merely convenient.

`Guid` values are **UUIDv7** (`Guid.CreateVersion7()`), which are time-ordered —
so they cluster well in B-tree indexes instead of scattering writes, and they can
be generated offline by a disconnected terminal without collision risk.

## Consequences

**Good**

- No converter boilerplate; EF Core, Npgsql and `System.Text.Json` handle `Guid`
  natively.
- Protection is enforced by the database, not by developer discipline.
- Ids remain generatable offline, which the sync design depends on.

**Bad**

- Argument-order mistakes are caught by tests rather than the compiler.
- Mitigation: `TenantIsolationTests` asserts by reflection that every entity
  implements `ITenantOwned` or appears on an explicit allow-list of shared
  reference data, and integration tests assert that tenant A cannot read tenant
  B's rows through the API.

## Revisit when

- A cross-tenant leak reaches a real customer despite RLS.
- The team grows and onboarding mistakes with id arguments become common.
