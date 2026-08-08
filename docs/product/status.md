# Build status

> **Purpose:** a project scaffold makes empty things look finished. This page is
> the honest inventory. Update it in the same commit that changes reality.

**Last updated:** 2026-08-08 · **Roadmap phase:** Month 0 — Foundations

## Legend

| | Meaning |
|---|---|
| ✅ | Built, tested, works |
| 🚧 | Partially built |
| 📁 | Empty project — exists so the structure is visible, contains no logic |
| ⬜ | Not started |

## Backend

| Component | State | Notes |
|---|---|---|
| Solution + build pipeline | ✅ | .NET 10, central package management, zero-warning build |
| `Brasa.Shared` — `Money` | ✅ | Integer cents, allocation-based splitting, 17 tests passing |
| `Brasa.Shared` — `Result`/`Error` | ✅ | Expected failures as values |
| `Brasa.Shared` — tenancy | ✅ | `ITenantContext`, resolve-once-per-scope `TenantContext` |
| `Brasa.Shared` — time | ✅ | `IClock`, `PortugueseRegion`, business-day calculation |
| `Brasa.Shared` — persistence base | ✅ | `Entity` (UUIDv7), `ITenantOwned`, `IAuditable`, `ISoftDeletable` |
| `Brasa.Shared` — outbox contracts | ✅ | Types defined; **no dispatcher implementation yet** |
| `Brasa.Api` | 🚧 | Boots. Serilog, ProblemDetails, OpenAPI, `/health`, `/api/v1/ping`. No DB, no auth |
| EF Core + PostgreSQL + RLS | ⬜ | Next up |
| `Modules.Identity` | 📁 | |
| `Modules.Catalog` | 📁 | |
| `Modules.Ordering` | 📁 | |
| `Modules.Fiscal` | 📁 | `IFiscalProvider` not yet defined |
| `Modules.Payments` | 📁 | |
| `Modules.Reporting` | 📁 | |
| `Fiscal.Portugal` | 📁 | **Nothing fiscal is implemented.** Month 4 |
| `Fiscal.Mock` | 📁 | Month 2 |

## Site Agent

| Component | State | Notes |
|---|---|---|
| `Brasa.SiteAgent` | 🚧 | Host starts and stops cleanly. Nothing else — Month 3 |
| Fiscal signing | ⬜ | |
| ESC/POS printing | ⬜ | |
| LAN REST + SignalR hub | ⬜ | |
| Cloud outbox sync | ⬜ | |

## Web clients

| Client | State |
|---|---|
| `pos` | ⬜ |
| `kds` | ⬜ |
| `admin` | ⬜ |
| `order` (QR self-ordering) | ⬜ |

## Tests

| Suite | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | 17 passing, incl. exhaustive allocation check |
| `Brasa.Fiscal.Portugal.Tests` | 📁 | Golden-file fixtures wired in csproj; no tests yet |
| `Brasa.Api.IntegrationTests` | 📁 | Testcontainers referenced and Docker available; no tests written yet |

## Infrastructure

| Component | State | Notes |
|---|---|---|
| Docker | ✅ | 29.6.2, Compose v5.3.1 |
| PostgreSQL | ✅ | 18.4 container, ICU locale provider, `pt-PT` |
| Seq (log viewer) | ✅ | `http://localhost:5341` |
| CI (GitHub Actions) | ✅ | Build gate, tests, vulnerability scan, docs link check |

## Blockers

| # | Blocker | Impact | Owner |
|---|---|---|---|
| 1 | Portuguese legal entity not yet formed | Cannot submit Modelo 24 to AT. **Start now — it is on the critical path for revenue, not for code** | Founder |
| 2 | VAT rules unconfirmed by an accountant | `TaxRule` design absorbs any answer, but rates must be verified before launch | Founder |
| 3 | `Brasa` trademark and domains not cleared | Check INPI (PT), EUIPO (classes 9/42), and `.pt`/`.com` before spending on branding | Founder |
