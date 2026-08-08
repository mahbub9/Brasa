# ADR 0010 — Split the database role: unprivileged at runtime, superuser only for migrations

**Status:** Accepted · **Date:** 2026-08-09

## Context

[ADR 0005](0005-plain-guid-ids.md) chose plain `Guid` ids specifically because
PostgreSQL row-level security — not compile-time typing — is the real tenant
isolation boundary. I0's first live test against a running API and a real
Postgres container was designed to prove that boundary actually holds, not just
that the migration SQL looked correct.

It did not hold. Querying directly as the application's database role, with the
session tenant variable set to a tenant that owned none of the data, still
returned every row. Setting no tenant at all also returned every row.

The cause: `docker-compose.yml` creates exactly one Postgres role, via
`POSTGRES_USER`. In the official Postgres image, that role is a **superuser**.
PostgreSQL superusers bypass row-level security unconditionally — the `USING`
and `WITH CHECK` clauses on every policy simply never evaluate for them. Crucially,
`FORCE ROW LEVEL SECURITY` (already present on every table, added deliberately
per ADR 0005) does not change this: FORCE only changes behaviour for the table
**owner**, and only when the owner is not a superuser. It has no effect on an
actual superuser regardless.

Every policy created by `RowLevelSecurity.EnableFor` was syntactically correct
and semantically inert, for every table, from the first migration.

## Decision

Two roles, two purposes:

| Role | Attributes | Used for |
|---|---|---|
| `brasa` (`POSTGRES_USER`) | Superuser | Running migrations only — DDL, creating RLS policies |
| `brasa_app` | Ordinary role, no `SUPERUSER`, no `BYPASSRLS` | Everything the running application does |

`infra/initdb/01-app-role.sql` creates `brasa_app` once, on first container
init. `RowLevelSecurity.EnableFor` now grants `USAGE`/`SELECT`/`INSERT`/
`UPDATE`/`DELETE` to `brasa_app` in the **same migration** that creates each
table and its policy — so a table is never briefly enabled-but-unreachable or
reachable-but-unpolicied.

Two connection strings follow: `ConnectionStrings:Postgres` (runtime,
`brasa_app`) is what `AddCatalogModule`/`AddOrderingModule` register for request
handling. `ConnectionStrings:PostgresMigrations` (`brasa`) is used only by a
dedicated `MigrateAsync` helper in `Program.cs`, built from throwaway
`DbContext` instances rather than the DI-registered ones — the registered
contexts are wired to the restricted role and cannot run DDL.

`dotnet ef migrations add` no longer depends on booting `Brasa.Api`'s
`Program.cs` at all: `CatalogDbContextFactory` and `OrderingDbContextFactory`
(`IDesignTimeDbContextFactory<T>`) build a context directly against the
migration connection. This was worth doing regardless of the role split,
because `Program.cs` already runs real startup behaviour (migrations, seeding)
that must never become a side effect of generating a migration.

## Consequences

**Good**

- Row-level security now actually isolates tenants. Verified directly: as
  `brasa_app`, no tenant set → zero rows; wrong tenant set → zero rows; owning
  tenant set → all rows; `DROP TABLE` → refused for lack of ownership.
- The runtime role's blast radius is now genuinely limited to DML. A bug or an
  injected query can read and write rows within RLS, but cannot alter schema,
  disable a policy, or grant itself more access.
- The design-time factories make migration generation independent of the
  application's growing startup surface.

**Bad**

- Two connection strings and two roles to keep straight. A future contributor
  who adds a new module's `AddXModule(connectionString)` call using the
  migrations connection string by copy-paste error would silently run the
  application with elevated privileges, defeating the entire point.
- The migration runner in `Program.cs` builds `DbContext` instances by hand
  rather than resolving them from DI, which is a second, parallel construction
  path to keep in sync with `CatalogModule`/`OrderingModule`'s registration.

**Mitigation for the "silently over-privileged module" risk:** the connection
string names themselves are the guardrail — `Postgres` vs `PostgresMigrations`
is deliberately not a subtle distinction. A future addition to this ADR would be
a startup assertion that queries `current_user` and fails fast if the runtime
process ever connects as the migration role.

## Revisit when

- A managed Postgres provider (e.g., a cloud offering that manages roles
  differently, or `rds_superuser`-style constrained superuser models) changes
  what "superuser" actually grants, in a way that affects this reasoning.
- The module count grows enough that hand-listing two connection strings per
  module becomes error-prone — at that point, add the `current_user` startup
  assertion described above rather than trusting naming alone.
