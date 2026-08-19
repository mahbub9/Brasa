# Deployment

> **Status: two working pilot paths, no Production path yet.** Everything on
> this page deploys a real, working, internet-reachable instance of Brasa —
> but as a **single-tenant pilot running under a non-Production environment
> name**, the same "parallel-run" shape [roadmap.md](../product/roadmap.md)
> describes for Month 1. A secured, multi-tenant, `ASPNETCORE_ENVIRONMENT=Production`
> deployment is not possible today — see [Why "Production" doesn't boot
> yet](#why-production-doesn-t-boot-yet) before you assume otherwise. This is
> the doc `OPS-11` (still ⬜ in [backlog.md](../product/backlog.md)) will
> eventually make redundant by scripting most of it; until then, this is the
> manual runbook.

## Which path do you need?

| | Path A — beta pilot | Path B — Postgres-backed |
|---|---|---|
| Setup time | Minutes | ~30–60 min, first time |
| Infrastructure | One server, .NET runtime only | One server, Docker + .NET runtime |
| Data survives a restart? | **No** — wiped every time (SQLite `:memory:`) | Yes — real PostgreSQL |
| Tenant isolation | None (single hardcoded tenant either way) | Real RLS, but still one tenant in practice |
| Backed by | [ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md) | `infra/docker-compose.yml`, the same stack [getting-started.md](getting-started.md) runs locally |
| Use it when | You need a restaurant looking at this **this week**, and can accept "don't restart mid-service" as an operational rule | You need the deployment to survive restarts/redeploys, or you're piloting for longer than a few days |

Both paths run the exact same code, behind the exact same `DevTenantMiddleware`
single-tenant shortcut. Neither is more "real" than the other from a security
or multi-tenancy standpoint — they differ only in whether data persists.

## Why "Production" doesn't boot yet

Two independent fail-closed guards throw `InvalidOperationException` the
moment `ASPNETCORE_ENVIRONMENT=Production`, and both are intentional, not
bugs to route around:

1. **No tenant resolution exists.** `DevTenantMiddleware`
   (`src/backend/Brasa.Api/Tenancy/DevTenantMiddleware.cs`)
   resolves every request to one hardcoded tenant and refuses to run at all
   in Production — real tenant resolution needs auth (`IDN-03`…`08`), which
   isn't built. A hardcoded tenant reaching Production would silently
   attribute every customer's data to one fixed id.
2. **No certified fiscal engine exists.** `AddMockFiscalProvider`
   (`src/backend/Brasa.Fiscal.Mock/FiscalMockServiceCollectionExtensions.cs`)
   refuses to register in Production: *"It produces structurally valid but
   fiscally meaningless documents."* `Brasa.Fiscal.Portugal` (the real,
   AT-certified engine) is still an empty project — see
   [docs/fiscal/README.md](../fiscal/README.md). Portuguese law fines
   €3,000–€18,750 per infraction for issuing invoices on uncertified
   software; failing closed here is the only acceptable behaviour.

`Program.cs` says this outright at the fiscal registration call site: *"there
is no code path that can legally issue a real document, so
`AddMockFiscalProvider`'s own Production guard is what keeps a Production
boot from ever succeeding today. That is correct: it should fail closed, not
silently serve mock documents to a real restaurant."*

Practically: run with `ASPNETCORE_ENVIRONMENT=Staging` (or any name other
than `Production`) for both paths below. The OpenAPI document and the HTTPS
redirect branch both key off `IsDevelopment()` specifically, so `Staging`
gets you the smaller, more production-shaped surface (no public
`/openapi/v1.json`, HTTPS redirect active) without tripping either guard
above.

**One thing does *not* shrink under `Staging`, and you should know before
you expose this to the internet**: the Hangfire dashboard (`/hangfire`) is
mapped under any environment that isn't literally `Production`
(`!IsProduction()`, not `IsDevelopment()`), and it has **no authorization
filter at all** — this app has no auth story yet, so there's nothing to gate
it behind today. Anyone who can reach `/hangfire` on your deployed API can
see and trigger every scheduled job. Block the path at the reverse proxy
instead — see [Fronting with Caddy](#fronting-with-caddy-both-paths) below.

## Path A — beta pilot (no database to run)

The whole point of [ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md):
*"A pilot restaurant needs nothing installed to run the beta — no Docker, no
Postgres, no `docker-compose up`."* Data lives in an in-process SQLite
`:memory:` store and is lost on every restart or crash — the one operational
rule is **don't restart during service**.

> On your own machine rather than a real server, `infra/scripts/start-local.ps1`
> does everything below in one command — see
> [getting-started.md](getting-started.md#quick-start-one-click). The steps
> here are what that script automates, for a real remote server where you
> don't have a PowerShell prompt already open on the target box.

### 1. Provision a server

Any small Linux VM reachable from the internet. A Hetzner CX22 (2 vCPU/4GB,
~€4/month) is what [plan.md](../product/plan.md)'s hosting choice assumes;
any equivalent works identically for this path since nothing here is
Hetzner-specific yet.

### 2. Install the ASP.NET Core runtime

```bash
# Ubuntu/Debian — see learn.microsoft.com/dotnet/core/install/linux for other distros
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
```

Framework-dependent, not self-contained — smaller to ship, and the runtime
patches independently of app deploys.

### 3. Publish and copy the API

From your dev machine:

```powershell
dotnet publish src/backend/Brasa.Api -c Release -o .\publish --self-contained false
```

Copy `.\publish\` to the server (`scp`, `rsync`, or your CI artifact of
choice) — e.g. `/opt/brasa/api/`.

### 4. Run it

```bash
cd /opt/brasa/api
ASPNETCORE_ENVIRONMENT=Staging \
ASPNETCORE_URLS=http://127.0.0.1:5216 \
Database__Provider=InMemory \
Database__SeedOnStartup=true \
dotnet Brasa.Api.dll
```

Binds to loopback only — a reverse proxy in front (see [Fronting with
Caddy](#fronting-with-caddy-both-paths)) is what the internet actually talks
to. `Database__SeedOnStartup=true` gets you the demo menu/floor/staff
immediately; once the pilot restaurant has entered their own real data via
`admin`, flip it to `false` so a future restart shows an empty store to
re-enter into, not a silent reseed over what they just typed in (data is
gone either way on restart — this only controls what greets them after).

For anything beyond a one-off manual test, run it as a `systemd` service —
see [Running as a systemd service](#running-as-a-systemd-service-both-paths)
below; the unit file there works unchanged for either path, just swap the
`Environment=` lines.

### 5. Build and deploy the web clients

See [Building and deploying the web clients](#building-and-deploying-the-web-clients-both-paths).

### What you lose on this path

- Every open table and order is gone on any restart or crash — see ADR 0012's
  own Consequences section for the full list (no RLS boundary, no
  optimistic-concurrency guard on seating a table, date-sorted list endpoints
  load their full result set into memory). All accepted trade-offs for a
  single pilot restaurant, not oversights.
- `DatabaseBackupJob`/`infra/scripts/backup-database.ps1` back up Postgres —
  there is nothing to back up here, by design.

## Path B — Postgres-backed

Closer to `OPS-11`'s intended target ("Hetzner + Caddy + Compose"), just
without the automation `OPS-11` would eventually add. Data survives restarts
and redeploys.

### 1. Provision a server and install prerequisites

Same server sizing note as Path A, but budget more headroom for Postgres —
a Hetzner CX32 (4 vCPU/8GB) is a more comfortable floor.

```bash
# Docker (for PostgreSQL + Seq — see infra/docker-compose.yml)
curl -fsSL https://get.docker.com | sudo sh

# ASP.NET Core runtime (same as Path A step 2)
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-10.0
```

### 2. Stand up PostgreSQL — with real credentials

Copy `infra/docker-compose.yml` and `infra/initdb/` to the server. **Do not
run them as committed** — every password in both is `devonly`/`devonly_app`/
`devonly_system`, deliberately, because they're dev-only and grant access to
nothing but a container on a developer's own machine (see the file's own
header comment). For a real deployment:

1. Generate three strong, distinct passwords (`brasa`/superuser,
   `brasa_app`/runtime, `brasa_system`/read-only cross-tenant — see
   [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) for
   why there are three roles at all, not two).
2. Edit `infra/initdb/01-app-role.sql` and `02-system-role.sql`, replacing
   `'devonly_app'`/`'devonly_system'` with the two you generated. These SQL
   files only run once, on the container's **first** boot against an empty
   data volume — get this right before `docker compose up`, not after.
3. Override `POSTGRES_PASSWORD` for the bootstrap `brasa` role via a
   `.env` file next to the compose file (gitignored — never commit real
   credentials) or an environment variable, rather than editing the
   committed default in place.

```bash
docker compose -f infra/docker-compose.yml up -d
```

The `seq` service is a local structured-log viewer with **no
authentication** (`SEQ_FIRSTRUN_NOAUTHENTICATION=true`, fine on a developer's
own machine where it binds to localhost only). On a real server, either drop
the port mapping entirely (logs still go to the console/systemd journal) or
put it behind the same reverse proxy with real auth in front — don't expose
port `5341` to the internet as-is.

### 3. Run migrations

`Program.cs`'s auto-migrate path only runs when `!IsProduction()` — true
here, since you're deploying under `Staging` — but it also seeds demo data by
default (`Database:SeedOnStartup`), which you likely don't want for a real
pilot's first boot. The cleaner option is running migrations explicitly,
once, before the app ever starts serving:

```powershell
dotnet tool install --global dotnet-ef   # once

# CatalogDbContextFactory and friends (IDesignTimeDbContextFactory<T>) read
# this environment variable directly, not a --connection flag passed to
# `dotnet ef` — the same convention getting-started.md's "Adding a
# migration" section already documents for `migrations add`.
$env:BRASA_MIGRATIONS_CONNECTION = "Host=<server>;Port=5432;Database=brasa;Username=brasa;Password=<the strong password from step 2>"

foreach ($ctx in @(
  @{ Project = "Brasa.Modules.Catalog";  Context = "CatalogDbContext" },
  @{ Project = "Brasa.Modules.Ordering"; Context = "OrderingDbContext" },
  @{ Project = "Brasa.Modules.Floor";    Context = "FloorDbContext" },
  @{ Project = "Brasa.Modules.Identity"; Context = "IdentityDbContext" },
  @{ Project = "Brasa.Modules.Payments"; Context = "PaymentsDbContext" }
)) {
  dotnet ef database update `
    --project "src/backend/$($ctx.Project)" `
    --startup-project src/backend/Brasa.Api `
    --context $ctx.Context
}
```

These five are every module with a real migrated schema today — `Fiscal` is
an `IFiscalProvider` contract with no EF Core persistence of its own yet, and
`Reporting` is still an empty project, so neither has a migration to run.
This is the exact same `dotnet ef` shape
[getting-started.md](getting-started.md) already uses for `migrations add`,
just `database update` against a remote connection string instead of the
local default.

If you'd rather let the app do it: set `Database__SeedOnStartup=false`,
start the app once under a non-Production name, let `MigrateAsync` run, then
proceed — functionally equivalent, just less explicit about what ran when.

### 4. Publish and run the API

Same publish step as Path A:

```powershell
dotnet publish src/backend/Brasa.Api -c Release -o .\publish --self-contained false
```

Copy to the server, then run (directly, or — recommended — as the `systemd`
service below) with the real Postgres connection strings:

```bash
ASPNETCORE_ENVIRONMENT=Staging \
ASPNETCORE_URLS=http://127.0.0.1:5216 \
Database__Provider=Postgres \
Database__SeedOnStartup=false \
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=brasa;Username=brasa_app;Password=<strong password>" \
ConnectionStrings__PostgresMigrations="Host=localhost;Port=5432;Database=brasa;Username=brasa;Password=<strong password>" \
ConnectionStrings__PostgresSystem="Host=localhost;Port=5432;Database=brasa;Username=brasa_system;Password=<strong password>" \
dotnet Brasa.Api.dll
```

`SeedOnStartup=false` here since you migrated explicitly in step 3 — leaving
it `true` would seed demo placeholders into the real database on first boot,
same trap Path A's own SQLite store avoids only by construction (wiping
everything anyway).

### 5. Build and deploy the web clients

See the shared section below.

### Backups

`infra/scripts/backup-database.ps1`/`restore-database.ps1`/`restore-drill.ps1`
default to a container named `brasa-postgres` and a database named `brasa` —
exactly what `docker compose -f infra/docker-compose.yml up -d` produces, so
**the scripts work unchanged against this deployment**, not only against a
developer's own machine. The one real constraint: they're PowerShell, so run
them from a Windows machine with Docker's remote context pointed at the
server (`docker context create <name> --docker "host=ssh://user@server"`,
then `docker context use <name>` before invoking the script), or port the
same `pg_dump`-inside-the-container-then-`docker cp`-out approach to a bash
script that runs on the server directly — the binary-safety reasoning in
`backup-database.ps1`'s own header (never pipe a binary `pg_dump` stream
through a text redirect) applies identically either way. See
[backup-and-restore.md](backup-and-restore.md) for the full mechanism.

`DatabaseBackupJob` (the Hangfire-scheduled version of the same scripts) is
guarded to never run outside a genuinely local dev Docker container today —
don't expect it to back up this deployment automatically until that's
revisited.

## Building and deploying the web clients (both paths)

`pos` and `admin` are the two clients that exist today (`kds`/`order` are
unbuilt — see [status.md](../product/status.md)). Both build to static files
— no Node server needed at runtime, per
[ADR 0004](../architecture/decisions/0004-react-pwa-not-blazor.md)'s own
"deployment stays trivial: static files behind Caddy or a CDN."

```powershell
cd src/web/pos
copy .env.example .env.production.local
# edit .env.production.local: VITE_API_BASE_URL=https://api.yourdomain.example/api/v1
npm install
npm run build      # tsc -b && vite build → dist/
```

Repeat for `src/web/admin`. Copy each `dist/` to the server (e.g.
`/opt/brasa/pos/`, `/opt/brasa/admin/`) — Caddy serves them directly, no
build step happens on the server itself.

Set `VITE_SENTRY_DSN` too if you have a real Sentry project (`OPS-14`) — both
`.env.example` files leave it empty on purpose since none exists yet in this
environment; it's inert either way, never a blocker.

## Fronting with Caddy (both paths)

```
# /etc/caddy/Caddyfile
api.yourdomain.example {
    # /hangfire has no authorization filter in the app itself (see the
    # warning above) — block it here rather than exposing it.
    handle /hangfire* {
        respond 404
    }
    reverse_proxy 127.0.0.1:5216
}

pos.yourdomain.example {
    root * /opt/brasa/pos
    file_server
    try_files {path} /index.html   # client-side routing fallback
}

admin.yourdomain.example {
    root * /opt/brasa/admin
    file_server
    try_files {path} /index.html
}
```

Caddy provisions and renews Let's Encrypt certificates automatically for
each domain — no manual TLS setup. Point each subdomain's DNS `A` record at
the server before starting Caddy, or the ACME challenge will fail.

**One real gap to fix before relying on this**: the API has no
`ForwardedHeadersMiddleware` registered. `Program.cs` calls
`app.UseHttpsRedirection()` for any non-Development environment (true for
`Staging`), but with Caddy terminating TLS and proxying to Kestrel over
plain HTTP, Kestrel sees every request as `http`, not `https` — triggering a
redirect on every single request, doubling latency, and leaving
`Request.IsHttps` wrong for anything that reads it. Add, before
`UseHttpsRedirection()` in `Program.cs`:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
```

and confirm Caddy sends `X-Forwarded-Proto` (it does by default via
`reverse_proxy`). This isn't wired in today — no deployment has needed it
until now — so treat it as a required change alongside your first real
Caddy-fronted deploy, not an optional hardening step.

## CORS

`Cors:AllowedOrigins` (only set in `appsettings.Development.json` today,
listing the two local Vite dev ports) must list your real public origins or
the browser blocks every request. Override via environment variables —
ASP.NET Core's config binder maps array entries by index:

```bash
Cors__AllowedOrigins__0=https://pos.yourdomain.example
Cors__AllowedOrigins__1=https://admin.yourdomain.example
```

## Running as a systemd service (both paths)

```ini
# /etc/systemd/system/brasa-api.service
[Unit]
Description=Brasa API
After=network.target docker.service

[Service]
WorkingDirectory=/opt/brasa/api
ExecStart=/usr/bin/dotnet /opt/brasa/api/Brasa.Api.dll
Restart=on-failure
User=brasa
EnvironmentFile=/opt/brasa/api/api.env

[Install]
WantedBy=multi-user.target
```

`/opt/brasa/api/api.env` (mode `600`, owned by the `brasa` user, never
committed — this is the environment-variable file referenced above, one
`KEY=value` per line, no quoting):

```
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://127.0.0.1:5216
Database__Provider=Postgres
Database__SeedOnStartup=false
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=brasa;Username=brasa_app;Password=...
ConnectionStrings__PostgresMigrations=Host=localhost;Port=5432;Database=brasa;Username=brasa;Password=...
ConnectionStrings__PostgresSystem=Host=localhost;Port=5432;Database=brasa;Username=brasa_system;Password=...
Cors__AllowedOrigins__0=https://pos.yourdomain.example
Cors__AllowedOrigins__1=https://admin.yourdomain.example
```

(Path A's env file just sets `Database__Provider=InMemory` and drops the
three `ConnectionStrings__*` lines — nothing else changes.)

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now brasa-api
sudo journalctl -u brasa-api -f   # tail logs
```

**On secret management**: this `.env`-file-with-restricted-permissions
approach is the honest, interim answer — `OPS-13` (a real secret manager) is
still ⬜, unbuilt. Nothing here is worse than what a real secret manager
would protect against beyond "don't commit the file and restrict its
filesystem permissions," but it is manual and doesn't rotate itself.

## Verifying a deployment

```bash
curl https://api.yourdomain.example/health         # liveness — no dependencies
curl https://api.yourdomain.example/health/ready    # readiness — checks PostgreSQL (Path B only; always healthy on Path A)
```

Then open `https://pos.yourdomain.example` and
`https://admin.yourdomain.example` in a browser and confirm each loads and
can reach the API (open a table, add an item — the same smoke test
[getting-started.md](getting-started.md) describes for local dev).

## Updating a running deployment

1. `dotnet publish` a new build, copy over the old `publish/` contents.
2. If the change added a migration: run the same `dotnet ef database
   update` loop from [step 3 of Path B](#_3-run-migrations) against the
   production connection string, **before** restarting the API — an old
   binary against a new schema is far safer than a new binary against an old
   one, since EF Core migrations are additive-by-convention in this codebase
   (see [module-boundaries.md](../architecture/module-boundaries.md) and
   `docs/architecture/api-contract.md`'s own breaking-change discipline).
3. `sudo systemctl restart brasa-api`.
4. Rebuild and re-copy any changed web client's `dist/` — no restart needed,
   Caddy serves the new files on the next request.

Path A loses all in-flight orders/tables on step 3's restart, by design —
schedule updates outside service hours.

## What this deployment is not

- **Not multi-tenant.** One hardcoded tenant, same as local dev — see
  [Why "Production" doesn't boot yet](#why-production-doesn-t-boot-yet).
- **Not authenticated.** Anyone who can reach `admin`'s or `pos`'s URL can do
  anything either app can do. Fine for a supervised pilot on a
  not-publicly-advertised URL; not fine for anything else. `IDN-03`…`08`
  (real auth) is what closes this gap.
- **Not fiscally certified.** Every fiscal document issued is a
  `MockFiscalProvider` fake — see
  [docs/fiscal/README.md](../fiscal/README.md). This deployment must never
  be used to invoice a real, live restaurant's actual service.
- **Not automated.** Every step above is manual. `OPS-11` is the task that
  would script this (and is the reason this whole page exists as a
  developer runbook rather than a `terraform apply`); `OPS-16` (a real
  staging environment, tested continuously) and `OPS-13` (real secret
  management) are both still ⬜ too.
- **File uploads are not tenant-scoped.** Menu item photos
  (`CAT-02`, `MenuItemImageStorage`) land in one shared local folder on
  whichever server the API runs on — a known, named gap, harmless only
  because there's one tenant to begin with.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| API throws `InvalidOperationException` mentioning `DevTenantMiddleware` or `MockFiscalProvider` on startup | `ASPNETCORE_ENVIRONMENT=Production` was set — use `Staging` or any other non-Production name (see above) |
| API throws `InvalidOperationException` mentioning `Database:Provider=InMemory` | Same as above, plus `Database__Provider=InMemory` — Path A cannot combine with a Production environment name either |
| `/health/ready` returns `503` (Path B) | PostgreSQL isn't reachable — check `docker compose -f infra/docker-compose.yml ps` and the three connection strings in the systemd `EnvironmentFile` |
| Browser console shows CORS errors | `Cors__AllowedOrigins__*` doesn't list the exact origin (scheme + host, no trailing slash) the browser is loading the web client from |
| Every request 307-redirects, or `Request.IsHttps` is unexpectedly `false` | The `ForwardedHeadersMiddleware` gap above — add it before `UseHttpsRedirection()` |
| `dotnet ef database update` fails to connect | Confirm you're using the **migrations** (`brasa`, superuser) connection string, not the runtime `brasa_app` one — `brasa_app` has no DDL rights by design (see [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md)) |
| A restart wiped everything (Path A) | Expected — see [What you lose on this path](#what-you-lose-on-this-path). If this is unacceptable, you need Path B |
