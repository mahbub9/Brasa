# ADR 0012 — A config-flag in-memory database for the pilot beta

**Status:** Accepted · **Date:** 2026-08-16

## Context

The plan changed from "build toward certification" to "get a real restaurant
using this and talking to us within 7 days" (see
[docs/product/roadmap.md](../../product/roadmap.md)'s "parallel-run mode",
pulled forward from Month 1 to Week 1). Installing and babysitting a real
PostgreSQL instance is unnecessary friction for a single-restaurant pilot —
the beta needs a database that requires nothing on-site, not a production
data store.

Every module's persistence was hardcoded to PostgreSQL in three places per
`DbContext` (`AddXModule`, `Program.cs`'s `MigrateAsync`, and each
`IDesignTimeDbContextFactory<T>`), with no existing seam to swap providers —
unlike `IFiscalProvider` (ADR 0002) or `IClock`, which already follow a
one-interface, swappable-implementation shape.

Two things make dropping to an in-memory store for the beta a smaller
decision than it first looks:

- **Row-level security is the real tenant boundary today**
  (`docs/architecture/multi-tenancy.md`), and it is PostgreSQL-only. But
  `DevTenantMiddleware` already resolves *every* request to one hardcoded
  tenant and refuses to run in Production at all — there is no second
  tenant for RLS to be protecting against in any environment RLS could be
  dropped in. The EF global query filter (already provider-agnostic) does
  100% of the isolation work that matters for a one-restaurant pilot,
  regardless of provider.
- Everything else module code touches is either provider-agnostic LINQ
  (seeders, domain logic) or `HasColumnType`/`HasColumnName` annotations —
  fine against any real relational provider, which is exactly why the
  provider choice below is not EF Core's own InMemory package.

Two things are *not* portable and needed their own answer regardless of
which in-memory approach was chosen: Hangfire's Postgres storage (a
dependency entirely outside EF Core) and `DatabaseHealthCheck`, which opens
a raw `NpgsqlConnection`.

**EF Core's own InMemory provider (`Microsoft.EntityFrameworkCore.InMemory`)
was tried first and rejected** — not a hypothetical concern, a real one:
booting against it and calling `GET /menu/all` threw
`System.Collections.Generic.KeyNotFoundException: The given key 'Property:
MenuItem.Price#Money.MinorUnits (long) Required' was not present in the
dictionary`. `Money` is mapped as a **required** `ComplexProperty`
(`ModelBuilderExtensions.MapMoney`, hard rule 1 — money is never a bare
`decimal`) everywhere in this codebase, and EF Core's InMemory provider has
a known class of bugs with required complex properties (see
`dotnet/efcore#32699` and related issues) that PostgreSQL and SQLite, both
real relational engines, don't share. This wasn't a one-off: the same
mapping shape is used for every money-bearing column in Catalog and
Ordering, so the InMemory provider was unusable for this codebase's core
invariant, not just inconvenient for one entity.

## Decision

One config flag, `Database:Provider` (`Postgres` | `InMemory`), bound once
into a `DatabaseOptions` singleton
(`Brasa.Shared/Persistence/DatabaseOptions.cs`) and read wherever a
provider-specific choice was previously hardcoded. `InMemory` is backed by
**SQLite's `:memory:` mode**, not EF Core's InMemory package — a real
relational engine, so `MapMoney`'s required `ComplexProperty` (and every
other relational annotation already written for Postgres) works unchanged:

- `Brasa.Shared/Persistence/ModulePersistenceExtensions.cs` — one shared
  `AddModuleDbContext<TContext>` helper all four `AddXModule` methods call
  into, so the Postgres/InMemory branch exists once, not four times. On the
  InMemory path it opens one `SqliteConnection("Data Source=:memory:")`
  **once, outside the per-scope `AddDbContext` factory**, and reuses that
  same open connection for every `DbContext` instance — SQLite's `:memory:`
  mode discards everything the moment its last connection closes, so the
  normal per-request open/close EF Core does would otherwise wipe the store
  between requests. `TenantSessionInterceptor` (sets a PostgreSQL session
  variable RLS reads) is only attached on the Postgres path — it's a no-op
  without RLS, so it's simply not registered rather than
  registered-and-ignored.
- `Program.cs`: `MigrateAsync` runs on the Postgres path; a new
  `EnsureCreatedAsync` local function (resolving the already DI-registered
  contexts — SQLite in-memory has no elevated-vs-runtime role split, unlike
  ADR 0010's Postgres one) builds each module's schema straight from the EF
  model on the InMemory path instead, since SQLite starts with zero tables
  too and there are no migrations to run against it. The nightly
  `DatabaseBackupJob` only runs on the Postgres path (nothing to back up
  otherwise). Hangfire's storage and the `/health/ready` database check
  both branch on the same flag — `InMemoryDatabaseHealthCheck` always
  reports healthy, keeping `/health/ready`'s shape (one check, tagged
  `"ready"`) identical either way.
- A second flag, `Database:SeedOnStartup` (default `true`, matching today's
  behavior exactly), exists because InMemory is wiped on every process
  restart — a beta pilot flips it to `false` once the restaurant's real
  menu/floor/staff has been entered via `admin`, so a restart shows an
  empty store to re-populate rather than silently reseeding demo
  placeholders over live data.
- Design-time factories (`CatalogDbContextFactory` etc.) are untouched —
  CLI-only, always Postgres, since InMemory has no migrations to author.
- Fail-closed guard, same shape as `AddMockFiscalProvider`'s: `Program.cs`
  throws at startup if `IsProduction() && Provider == InMemory`.
- `Brasa.Modules.Floor/Persistence/FloorDbContext.cs` — a second real,
  encountered incompatibility, not a hypothetical one: `TableConfiguration`
  maps `Table`'s optimistic-concurrency token to `xmin`, PostgreSQL's
  built-in row-version system column (exists on every row, no migration
  needed, no app write ever required). SQLite has no such column and no way
  to generate one, which failed every insert — including seeding — with a
  `NOT NULL constraint failed: tables.xmin` error. `OnModelCreating` now
  calls `modelBuilder.Entity<Table>().Ignore("xmin")` for any non-Postgres
  provider, dropping the compare-and-swap guard on `Table.Occupy()` there —
  the same class of accepted trade-down as the RLS drop above: a
  single-restaurant pilot has far lower concurrent-occupy pressure than
  production multi-terminal use.

Default stays `Postgres` in `appsettings.json` — nothing changes for the
existing dev/test/Postgres path unless explicitly overridden. The beta run
uses a new `beta` profile in `Properties/launchSettings.json`
(`Database__Provider=InMemory`), not a new `ASPNETCORE_ENVIRONMENT` value —
`IsDevelopment()`/`IsProduction()` already gate several unrelated things
(OpenAPI, HTTPS redirect, Hangfire dashboard) that shouldn't move just
because the database provider changed.

## Consequences

**Good**

- A pilot restaurant needs nothing installed to run the beta — no Docker,
  no Postgres, no `docker-compose up`. `dotnet run --launch-profile beta`
  is the entire setup.
- Swapping back to Postgres for production is exactly flipping the flag —
  see the checklist below, not a re-code.
- The pattern matches this repo's existing swappable-implementation shape
  (`IFiscalProvider`, `IClock`) rather than inventing a new one.

**Bad**

- **In-memory data is wiped on every process restart or crash** — every
  open table and order, mid-service. This is the real operational risk of
  the whole approach; there is no code-level mitigation, only an
  operational one (don't redeploy during service; treat any unplanned
  restart as "re-enter today's live state").
- No RLS boundary while `InMemory` — acceptable only because
  `DevTenantMiddleware` already makes every non-Production environment
  single-tenant regardless (see Context above), not because RLS stopped
  mattering. **This reasoning breaks the moment a second real tenant
  exists** — InMemory must not survive past this one-restaurant pilot.
- Two config flags (`Provider`, `SeedOnStartup`) a future contributor needs
  to know about, on top of the two-connection-string split ADR 0010
  already added.
- **No optimistic-concurrency guard on `Table.Occupy()` while `InMemory`** —
  two terminals racing to open the same table would no longer both fail
  down to a clean 409; the second writer could silently win a blind update.
  Low-probability for one pilot restaurant's terminal count, but a real,
  intentional loss versus Postgres, not just a theoretical one — see the
  `xmin`/`FloorDbContext` note in Context above.
- The kept-alive `SqliteConnection` per module is never explicitly disposed
  — it lives for the process's lifetime by design (closing it would wipe
  the store), so there's no clean shutdown path for it. Acceptable for a
  short-lived beta process where exit reclaims everything; would need a
  real answer if this pattern were ever reused somewhere longer-lived.

## Swapping back to Postgres for production

Confirmed to genuinely be "flip the flag," plus re-verifying, not re-coding:

1. Set `Database:Provider` back to `Postgres` (or simply omit the beta
   profile) and re-run `Database.MigrateAsync()` against the real target.
2. Re-run `tests/Brasa.Api.IntegrationTests/TenantIsolationIntegrationTests.cs`
   (RLS via Testcontainers) explicitly before trusting the cutover — it's
   untouched by this whole change (builds its own context directly via
   `CatalogDbContextFactory`), so it's the right pre-flight check.
3. Confirm `infra/initdb/01-app-role.sql` (creates `brasa_app`) has been
   applied to the production instance, and that Hangfire's Postgres storage
   and `PostgresMigrations` connection are reachable.
4. Remove the `Database__Provider=InMemory` override everywhere it was
   set — backed by the fail-closed guard so a leak fails loudly, not
   silently.
5. No in-memory beta data carries forward automatically. Whether the
   pilot's real menu/floor/staff gets exported and replayed into Postgres,
   or re-entered, is a decision for whoever runs that cutover — not solved
   by this ADR.

## Revisit when

- A second real tenant is onboarded — `InMemory` (and this beta's
  single-tenant RLS reasoning) must not still be in use at that point.
- The pilot restaurant's live data needs to migrate into a real Postgres
  instance at go-live — decide the export/replay approach then.
