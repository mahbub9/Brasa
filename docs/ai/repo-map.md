# Repository map

> Every tracked file, with its purpose and state. Kept current so a new session
> can locate work without reading the tree. **Update this in the same commit as
> any file added, moved, or deleted.**

**Last verified:** 2026-08-08 · Companion to [README.md](README.md)

**Legend:** ✅ implemented · 🚧 partial · 📁 empty project, structure only · ⬜ planned

---

## Root

| File | Purpose |
|---|---|
| `Brasa.slnx` | Solution. **.NET 10 XML format — not `.sln`** |
| `Directory.Build.props` | Solution-wide MSBuild. Nullable, `TreatWarningsAsErrors`, `InvariantGlobalization=false` (ICU needed for pt-PT and Azores) |
| `Directory.Packages.props` | Central package management. All versions live here; `.csproj` reference by name only |
| `.editorconfig` | Style + analyzer policy. Every suppression carries a written reason |
| `.gitattributes` | Line-ending normalisation. Fiscal fixtures marked `-text` (byte-exact) |
| `.gitignore` | Blocks keys, certs, `.env`, local DBs |
| `README.md` | Project front door |
| `CLAUDE.md` | Rules for Claude Code sessions |
| `CONTRIBUTING.md` | Workflow, commit format, documentation contract |

## `.github/`

| File | Purpose |
|---|---|
| `workflows/ci.yml` | Build (zero-warning gate) + test + transitive vulnerability scan |
| `workflows/docs.yml` | Relative-link check; warns when source changes without a `status.md` update |
| `pull_request_template.md` | Docs checklist, plus fiscal and money sections |
| `ISSUE_TEMPLATE/bug.md` | Bug report; severity includes a **Fiscal** tier |
| `ISSUE_TEMPLATE/feature.md` | Feature request; forces an offline-behaviour answer |
| `ISSUE_TEMPLATE/fiscal-change.md` | Fiscal change; requires citing the legal instrument |

## `src/backend/Brasa.Shared` — the shared kernel ✅

Depended on by every module; depends on no module. Deliberately small.

| File | State | Contents |
|---|---|---|
| `Primitives/Money.cs` | ✅ | Integer minor units. `Allocate` for splitting, `Format(culture)` for display, `ToString()` invariant. **The most important type in the system** |
| `Primitives/CurrencyCode.cs` | ✅ | `Eur = 0` so `default(Money)` is zero euros. Seam for future non-euro markets |
| `Primitives/Result.cs` | ✅ | `Result`, `Result<T>` with `Match` |
| `Primitives/Error.cs` | ✅ | `Error` + `ErrorType` → maps to HTTP status at the API edge |
| `Tenancy/ITenantContext.cs` | ✅ | Tenant / site / terminal / user for the current scope |
| `Tenancy/TenantContext.cs` | ✅ | Resolve-once-per-scope; throws on reassignment |
| `Time/IClock.cs` | ✅ | `IClock`, `SystemClock`. Nothing may call `DateTime.UtcNow` |
| `Time/PortugueseTimeZone.cs` | ✅ | `PortugueseRegion` (Continental/Madeira/Azores) + business-day calculation |
| `Persistence/Entity.cs` | ✅ | `IEntity`, `ITenantOwned`, `IAuditable`, `ISoftDeletable`, `Entity` (UUIDv7) |
| `Messaging/IntegrationEvent.cs` | ✅ | Event, handler and dispatcher contracts. **Only sanctioned cross-module channel** |
| `Messaging/OutboxMessage.cs` | ✅ | Transactional outbox row. Also the offline sync mechanism |

⬜ **Missing:** the dispatcher implementation, EF Core configuration, DI registration.

## `src/backend/Brasa.Api` 🚧

| File | State | Contents |
|---|---|---|
| `Program.cs` | 🚧 | Serilog, ProblemDetails, OpenAPI, `/health`, `/api/v1/ping`. `partial class Program` so `WebApplicationFactory` can boot it. **No DB, no auth** |
| `appsettings.json` | ✅ | Serilog console sink, Postgres connection string |
| `appsettings.Development.json` | ✅ | Debug level, adds Seq sink at `localhost:5341` |

## `src/backend/Brasa.Modules.*` 📁

All six are **empty projects** — `.csproj` only, zero source files. They exist so
the structure is visible and the boundary rule is enforced by project references.

| Module | Will own | Roadmap |
|---|---|---|
| `Identity` | Users, roles, staff PINs, terminal pairing | Month 0–1 (next) |
| `Catalog` | Menu, modifiers, price lists, `TaxRule` | Month 1 |
| `Ordering` | Orders, tables, courses, splits, transfers | Month 2 |
| `Fiscal` | `IFiscalProvider`, document lifecycle, series, audit | Month 4 |
| `Payments` | Tenders, cash sessions, tips | Month 4 |
| `Reporting` | Read models, X/Z, VAT summaries | Month 5 |

Each references **only** `Brasa.Shared`. Never each other.

## `src/backend/Brasa.Fiscal.*` 📁

| Project | State | Notes |
|---|---|---|
| `Fiscal.Portugal` | 📁 | ATCUD, RSA chain, QR, SAF-T, AT webservices. **The certification subject.** Month 4 |
| `Fiscal.Mock` | 📁 | Deterministic fake so the POS can be built before certification. **Must never run in Production** |

## `src/agent/Brasa.SiteAgent` 🚧

| File | State | Contents |
|---|---|---|
| `Program.cs` | ✅ | Generic host bootstrap |
| `Worker.cs` | 🚧 | Starts and stops cleanly. Documents the four planned responsibilities and nothing more |

⬜ **Missing (Month 3):** SQLite store, fiscal signing, ESC/POS printing, LAN
REST + SignalR hub, cloud outbox sync.

## `tests/`

| Project | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | `Primitives/MoneyTests.cs` — 17 tests, exhaustive allocation over ~12,000 combinations |
| `Brasa.Fiscal.Portugal.Tests` | 📁 | Golden-file fixture copying wired in `.csproj`; no tests yet |
| `Brasa.Api.IntegrationTests` | 📁 | Testcontainers + `Mvc.Testing` referenced; no tests yet |

## `infra/`

| File | Purpose |
|---|---|
| `docker-compose.yml` | PostgreSQL 18 (ICU, `pt-PT`) + Seq. Mounts `/var/lib/postgresql`, **not** `.../data` |

## `docs/`

| Path | Purpose |
|---|---|
| `README.md` | Documentation index |
| `ai/README.md` | **AI session brief — the entry point for a new session** |
| `ai/repo-map.md` | This file |
| `glossary.md` | Portuguese fiscal and restaurant terminology |
| `architecture/README.md` | System overview: three tiers, ownership, shared kernel |
| `architecture/money.md` | Why integer cents; allocation, rounding, formatting |
| `architecture/multi-tenancy.md` | Query filters + RLS; the system context |
| `architecture/module-boundaries.md` | The five rules modules obey |
| `architecture/site-agent.md` | In-restaurant process design (stub status) |
| `architecture/conventions.md` | Code conventions, build policy, suppression register |
| `architecture/decisions/0001..0006` | ADRs — each with a "Revisit when" trigger |
| `fiscal/README.md` | ATCUD, signature chain, QR, SAF-T, document types, VAT |
| `fiscal/certification.md` | AT process, prerequisites, what AT verifies |
| `fiscal/key-management.md` | Signing key custody and open questions |
| `development/getting-started.md` | Prerequisites, build, run, local infra |
| `development/testing.md` | The four-tier testing bar |
| `development/documentation.md` | The documentation contract |
| `features/` | Per-feature documentation, one page each |
| `product/plan.md` | Approved build plan and 6-month roadmap |
| `product/status.md` | **Honest inventory of what is built** |

## Not yet created

| Path | Purpose | Roadmap |
|---|---|---|
| `web/pos` | POS PWA (React + TS + Vite, offline-first) | Month 2 |
| `web/kds` | Kitchen display | Month 3 |
| `web/admin` | Back-office SPA | Month 1 |
| `web/order` | QR self-ordering | Month 5 |
| `web/ui` | Shared component library | Month 1 |
| `web/sdk` | TypeScript client generated from OpenAPI | Month 1 |
