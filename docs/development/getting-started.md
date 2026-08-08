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
