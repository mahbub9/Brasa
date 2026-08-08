# Getting started

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | **10.0.302** or later | Backend, Site Agent |
| Node.js | **24.x** (npm 11) | Web clients (`pos`, `kds`, `admin`, `order`) |
| Docker | **29.x** | PostgreSQL for local dev, and Testcontainers for integration tests |
| Git | 2.4x | — |

Verify:

```powershell
dotnet --list-sdks
node --version; npm --version
docker --version
```

## Build and test

```powershell
dotnet build Brasa.slnx
dotnet test  Brasa.slnx
```

The build runs with `TreatWarningsAsErrors`. That is deliberate — it is how the
transitive `SQLitePCLRaw` CVE was caught on day one. If a warning blocks you, fix
it or suppress it **in `.editorconfig` with a written reason**; do not disable the
policy.

## Verified environment

The primary development machine (Windows 10 Home 19045) currently runs:

| Tool | Version |
|---|---|
| .NET SDK | 10.0.302 |
| Node / npm | 24.18.1 / 11.16.0 |
| Docker | 29.6.2 (Compose v5.3.1) |
| PostgreSQL | 18.4 in container, ICU `pt-PT` |
| Git | 2.54.0 |

Docker is required, not optional: `Testcontainers.PostgreSql` needs a daemon,
and verifying row-level security behaviour against a real, disposable database
is a core part of the testing bar. See [testing.md](testing.md).

## Running the API

```powershell
dotnet run --project src/backend/Brasa.Api
```

- `GET /health` — liveness
- `GET /api/v1/ping` — cheap reachability check the POS uses to decide whether
  the cloud is available
- `GET /openapi/v1.json` — OpenAPI document (Development only)

## Running the POS web shell

```powershell
cd src/web/pos
npm install
npm run dev
```

Opens on `http://localhost:5173`. It talks to the API at
`http://localhost:5216/api/v1` by default (the `http` launch profile) — override
with `VITE_API_BASE_URL` in a `.env.development.local` (see `.env.example`) if
your API runs elsewhere. The API's `Cors:AllowedOrigins` config
(`appsettings.Development.json`) must include the dev server's origin or the
browser will block every request; `http://localhost:5173` is already there.

**I0 scope:** one screen, no auth, every request attributed to the fixed dev
tenant by `DevTenantMiddleware`. It exists to prove the API end-to-end in a
browser, not as the real POS UX — see
[docs/product/status.md](../product/status.md).

## Local infrastructure

```powershell
docker compose -f infra/docker-compose.yml up -d
```

Starts PostgreSQL 18 and Seq (structured log viewer, at `http://localhost:5341`).
Credentials are development-only and committed on purpose; they grant access to
nothing but a local container.

**First run only:** `infra/initdb/01-app-role.sql` creates the `brasa_app`
runtime role automatically — but only against a fresh, empty data volume. If
you already had a `pgdata` volume from before this existed, recreate it:

```powershell
docker compose -f infra/docker-compose.yml down -v
docker compose -f infra/docker-compose.yml up -d
```

**Why two roles.** `brasa` (`POSTGRES_USER`) is a Postgres **superuser**, and
superusers bypass row-level security unconditionally — this was discovered the
hard way, see [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md).
The app connects as `brasa_app` (`ConnectionStrings:Postgres`) for everything;
`brasa` (`ConnectionStrings:PostgresMigrations`) is used only to run migrations.
If RLS ever appears to "not work" locally, check which role the query actually
ran as before suspecting the policy.

## Adding a migration

Each module owns its EF Core migrations under its own `Persistence/Migrations/`
folder. Install the tool once:

```powershell
dotnet tool install --global dotnet-ef
```

Then, for example, for Catalog:

```powershell
dotnet ef migrations add <Name> `
  --project src/backend/Brasa.Modules.Catalog `
  --startup-project src/backend/Brasa.Api `
  --context CatalogDbContext `
  --output-dir Persistence/Migrations
```

This uses `CatalogDbContextFactory`
(`IDesignTimeDbContextFactory<CatalogDbContext>`), not `Brasa.Api`'s
`Program.cs` — migration generation never depends on the app's startup
behaviour (seeding, etc.). It connects with the **migration** role by default;
override with the `BRASA_MIGRATIONS_CONNECTION` environment variable if your
local setup differs from the default in `infra/docker-compose.yml`.

**Every new tenant-owned table's migration must call
`migrationBuilder.EnableFor(table, schema)`** in `Up()` (and `DisableFor` in
`Down()`) — see [multi-tenancy.md](../architecture/multi-tenancy.md). A table
created without it has no RLS policy at all, which is worse than one with a
broken policy: at least a broken one is visibly present to review.

Check whether a migration is actually needed before writing one by hand:

```powershell
dotnet ef migrations has-pending-model-changes --project src/backend/Brasa.Modules.Catalog --startup-project src/backend/Brasa.Api --context CatalogDbContext
```

## Project conventions

Read [../architecture/conventions.md](../architecture/conventions.md) before your
first pull request. The two rules most often broken by newcomers:

1. **Never use `decimal` or `double` for money.** Use `Money`. See
   [../architecture/money.md](../architecture/money.md).
2. **Never call `DateTime.UtcNow` directly.** Inject `IClock`. Fiscal documents
   carry a `SystemEntryDate` that must be monotonic within a series, and the
   signature chain is only testable if time is injectable.
