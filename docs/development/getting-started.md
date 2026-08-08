# Getting started

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | **10.0.302** or later | Backend, Site Agent |
| Node.js | **24.x** (npm 11) | Web clients (`pos`, `kds`, `admin`, `order`) |
| Docker | any recent | PostgreSQL for local dev, and Testcontainers for integration tests |
| Git | 2.4x | — |

Verify:

```powershell
dotnet --list-sdks
node --version; npm --version
docker --version
```

## Build and test

```powershell
dotnet build RestaurantPos.slnx
dotnet test  RestaurantPos.slnx
```

The build runs with `TreatWarningsAsErrors`. That is deliberate — it is how the
transitive `SQLitePCLRaw` CVE was caught on day one. If a warning blocks you, fix
it or suppress it **in `.editorconfig` with a written reason**; do not disable the
policy.

## ⚠️ Known gap: Docker is not yet installed on the primary dev machine

At the time of writing, the development machine (Windows 10 Home 19045) has:

- ✅ .NET SDK 10.0.302
- ✅ Node 24.18.1 / npm 11.16.0
- ✅ Git 2.54.0
- ❌ **Docker — not installed**
- ❌ **WSL — `wsl.exe` present but no distribution installed**

Consequences until this is resolved:

- `infra/docker-compose.yml` cannot start local PostgreSQL.
- The **integration test project cannot run** — `Testcontainers.PostgreSql`
  requires a Docker daemon. Unit tests (`RestaurantPos.Shared.Tests`,
  `RestaurantPos.Fiscal.Portugal.Tests`) are unaffected and run today.

### Resolving it — pick one

**Option A — Docker Desktop (recommended).** Enables `docker-compose` *and*
Testcontainers, which the testing strategy depends on. Requires WSL2:

```powershell
# Elevated PowerShell. Reboots.
wsl --install
# then install Docker Desktop and enable the WSL2 backend
```

**Option B — native PostgreSQL on Windows.** Unblocks local development, but
**not** the integration tests. Only a stopgap: verifying row-level security
behaviour is a core part of the testing bar and needs a real, disposable database
per run. See [testing.md](testing.md).

## Running the API

```powershell
dotnet run --project src/backend/RestaurantPos.Api
```

- `GET /health` — liveness
- `GET /api/v1/ping` — cheap reachability check the POS uses to decide whether
  the cloud is available
- `GET /openapi/v1.json` — OpenAPI document (Development only)

## Local infrastructure

```powershell
docker compose -f infra/docker-compose.yml up -d
```

Starts PostgreSQL 18 and Seq (structured log viewer, http://localhost:5341).
Credentials are development-only and committed on purpose; they grant access to
nothing but a local container.

## Project conventions

Read [../architecture/conventions.md](../architecture/conventions.md) before your
first pull request. The two rules most often broken by newcomers:

1. **Never use `decimal` or `double` for money.** Use `Money`. See
   [../architecture/money.md](../architecture/money.md).
2. **Never call `DateTime.UtcNow` directly.** Inject `IClock`. Fiscal documents
   carry a `SystemEntryDate` that must be monotonic within a series, and the
   signature chain is only testable if time is injectable.
