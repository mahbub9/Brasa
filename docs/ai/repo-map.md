# Repository map

> Every tracked file, with its purpose and state. Kept current so a new session
> can locate work without reading the tree. **Update this in the same commit as
> any file added, moved, or deleted.**

**Last verified:** 2026-08-09 · Companion to [README.md](README.md)

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
| `package.json` | **Docs site only** — VitePress. The .NET build does not use npm |

## `.github/`

| File | Purpose |
|---|---|
| `workflows/ci.yml` | Build (zero-warning gate) + test + transitive vulnerability scan + `e2e` job (Playwright, `src/web/e2e` — written, **not yet exercised by an actual CI run**) |
| `workflows/docs.yml` | Relative-link check across all markdown; warns when source changes without a `status.md` update |
| `workflows/pages.yml` | Builds the VitePress site from `docs/` and deploys to GitHub Pages |
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
| `Persistence/Entity.cs` | ✅ | `IEntity`, `ITenantOwned`, `IAuditable`, `ISoftDeletable`, `Entity` (UUIDv7, `ValueGeneratedNever` — see [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) for why that matters) |
| `Persistence/TenantAwareDbContext.cs` | ✅ | Base `DbContext` every module extends. Applies query filters, stamps tenant/audit fields on save, guards against tenant reassignment |
| `Persistence/RowLevelSecurity.cs` | ✅ | `EnableFor`/`DisableFor` — emits the real RLS policy **and** grants the runtime role access, in the same migration |
| `Persistence/TenantSessionInterceptor.cs` | ✅ | Sets the `brasa.tenant_id` Postgres session variable RLS policies read, per connection |
| `Persistence/ModelBuilderExtensions.cs` | ✅ | `ApplyTenantQueryFilters`, `MapMoney`, `ApplyEntityConventions` (incl. the `ValueGeneratedNever` fix) |
| `Persistence/PersistenceServiceCollectionExtensions.cs` | ✅ | `AddBrasaTenancy()` — one call wires tenancy + the session interceptor for a host |
| `Tenancy/TenantContextAccessor.cs` | ✅ | `AsyncLocal`-backed singleton so EF's cached compiled model doesn't permanently capture one request's tenant |
| `Messaging/IntegrationEvent.cs` | ✅ | Event, handler and dispatcher contracts. **Only sanctioned cross-module channel** |
| `Messaging/OutboxMessage.cs` | ✅ | Transactional outbox row. Also the offline sync mechanism |

⬜ **Missing:** the dispatcher implementation.

## `src/backend/Brasa.Api` ✅ I0 walking skeleton

| File | State | Contents |
|---|---|---|
| `Program.cs` | ✅ | Two-role DI wiring (runtime vs. migration connection — [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md)), API versioning, CORS (`Cors:AllowedOrigins`, for web clients), migration runner, dev seeding, full middleware pipeline |
| `Tenancy/DevTenantMiddleware.cs` | ✅ | Attributes every request to one hardcoded tenant. **The entire auth story until IDN-03…08 (I3).** Throws if `IsProduction()` |
| `Idempotency/IdempotencyMiddleware.cs` | ✅ | Requires `Idempotency-Key` on mutating `/api` requests; replays the cached response on repeat. In-memory, per-instance — durable store needed before scaling out |
| `ErrorMapping.cs` | ✅ | The only place `ErrorType` → HTTP status is decided |
| `Endpoints/CatalogEndpoints.cs` | ✅ | `GET /menu` |
| `Endpoints/OrderEndpoints.cs` | ✅ | `POST /orders`, `GET /orders/{id}`, `POST /orders/{id}/lines`, `GET /orders/{id}/split`, `POST /orders/{id}/close` |
| `Contracts/*.cs` | ✅ | `MoneyDto`, `MenuItemDto`/`MenuCategoryDto`, `OrderDto`/`OrderLineDto`, `FiscalDocumentDto` + mappings |
| `Seed/DevCatalogSeeder.cs` | ✅ | Seeds a Portuguese demo menu spanning both VAT bands. Guarded the same way as the mock fiscal provider |
| `appsettings.json` | ✅ | **Two** connection strings — `Postgres` (runtime, `brasa_app`) and `PostgresMigrations` (`brasa`, superuser) |

## `src/backend/Brasa.Modules.*`

| Module | State | Roadmap |
|---|---|---|
| `Identity` | 📁 empty | I3 |
| `Catalog` | ✅ `MenuCategory`, `MenuItem`, `VatRate` (I0 placeholder for I1's full `TaxRule`), EF config + migration + design-time factory | — |
| `Ordering` | ✅ `Order` aggregate, `OrderLine`, `OrderStatus`; EF config + migration + design-time factory | — |
| `Fiscal` | ✅ `IFiscalProvider`, `FiscalDocument`, `FiscalDocumentLine` (VAT-inclusive derivation), `FiscalDocumentRequest`, `FiscalDocumentType` | — |
| `Payments` | 📁 empty | I6 |
| `Reporting` | 📁 empty | I8 |

Each references **only** `Brasa.Shared`. Never each other. `Brasa.Api`'s
`OrderEndpoints` composes Catalog + Ordering + Fiscal in one handler — that
composition is the API layer's job, not something the modules do to each other.

## `src/backend/Brasa.Fiscal.*`

| Project | State | Notes |
|---|---|---|
| `Fiscal.Portugal` | 📁 | ATCUD, RSA chain, QR, SAF-T, AT webservices. **The certification subject.** I7 |
| `Fiscal.Mock` | ✅ | `MockFiscalProvider` — deterministic, every value `MOCK-`-prefixed. `FiscalMockServiceCollectionExtensions` throws if registered under `IsProduction()` |

## `src/agent/Brasa.SiteAgent` 🚧

| File | State | Contents |
|---|---|---|
| `Program.cs` | ✅ | Generic host bootstrap |
| `Worker.cs` | 🚧 | Starts and stops cleanly. Documents the four planned responsibilities and nothing more |

⬜ **Missing (Month 3):** SQLite store, fiscal signing, ESC/POS printing, LAN
REST + SignalR hub, cloud outbox sync.

## `src/web/pos` ✅ I0 shell

React 19 + Vite 8 + TypeScript. One screen, no auth, no offline — proves the
API in a browser. Hand-written API layer (`src/api/`) is a placeholder for
`web/sdk` (WEB-03, generated from OpenAPI) once a second client app needs it.

| File | State | Contents |
|---|---|---|
| `src/App.tsx` | ✅ | Orchestrates the three phases: open table → order (menu + summary) → receipt |
| `src/api/client.ts` | ✅ | `fetch` wrapper; `ApiError` carries the `ProblemDetails.code`; every mutation gets its own `Idempotency-Key` via `crypto.randomUUID()` |
| `src/api/types.ts` | ✅ | Hand-written mirror of `Brasa.Api/Contracts/*.cs` — kept in sync manually until WEB-03 |
| `src/components/*.tsx` | ✅ | `OpenTableForm`, `MenuGrid`, `OrderSummary`, `Receipt`, `ErrorBanner`, `LanguageToggle` |
| `src/lib/money.ts` | ✅ | `Intl.NumberFormat('pt-PT', …)` — never formats `Money` by hand, and deliberately never follows the language toggle (see [ADR 0011](../architecture/decisions/0011-i18n.md)) |
| `src/i18n/i18n.ts` | ✅ | i18next config — pt default, en toggle (WEB-13) |
| `src/i18n/languageStorage.ts` | ✅ | `LanguageStore` interface + `cookieLanguageStore`; the seam a mobile client swaps for `AsyncStorage` |
| `src/i18n/resources/{pt,en}.ts` | ✅ | UI copy. Menu item names and money are **not** here — see the ADR |
| `.env.example` | ✅ | Documents `VITE_API_BASE_URL`; defaults to the API's `http` launch profile |

⬜ **Missing:** auth, offline (Dexie), floor plan, everything past I0 — see the
`WEB` epic in [backlog.md](../product/backlog.md).

## `src/web/e2e` ✅ I0 harness

Playwright + TypeScript, chromium only. `playwright.config.ts`'s `webServer`
starts both the API (`dotnet run --no-build`) and the `pos` dev server
itself — Docker (PostgreSQL) is the only thing it doesn't start. Verified
locally from both a warm state and a hard cold start; 6/6 passing either way.
See [../development/e2e-testing.md](../development/e2e-testing.md) for the
QA-01 decision record and what QA-04/06/07/08 are still blocked on.

| File | State | Contents |
|---|---|---|
| `tests/walking-skeleton.spec.ts` | ✅ | QA-05 — drives the real `pos` UI: open table → ring up → split preview → close → receipt. Runs in Portuguese, the app's default |
| `tests/split-preview.spec.ts` | ✅ | API-level (no browser); sweeps `Money.Allocate` across 1/2/3/5/7-way splits |
| `tests/language-toggle.spec.ts` | ✅ | WEB-13 — default language, the pt→en toggle, cookie attributes (`Path`, `SameSite`, not `httpOnly`) surviving a reload, and money staying `pt-PT` in English mode |
| `tests/support/api.ts` | ✅ | QA-03 test-data builders. Looks menu items up **by name**, never by id (ids are UUIDv7, not stable across a fresh database) |

## `tests/`

| Project | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | `Primitives/MoneyTests.cs` — 17 tests, exhaustive allocation over ~12,000 combinations |
| `Brasa.Fiscal.Portugal.Tests` | ✅ | 13 tests: `FiscalDocumentLineTests` (gross→net VAT derivation, exhaustive per rate — the regression test for the I0 VAT bug), `MockFiscalProviderTests` (per-tenant sequential numbering, mock markers, mixed-rate reconciliation) |
| `Brasa.Api.IntegrationTests` | 📁 | Testcontainers + `Mvc.Testing` referenced; no tests yet |

## `infra/`

| Path | Purpose |
|---|---|
| `docker-compose.yml` | PostgreSQL 18 (ICU, `pt-PT`) + Seq. Mounts `/var/lib/postgresql`, **not** `.../data`. Mounts `initdb/` for first-run role setup |
| `initdb/01-app-role.sql` | Creates `brasa_app` — the unprivileged runtime role. Runs once, on first container init only. See [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |

## `docs/`

Published to <https://mahbub9.github.io/Brasa/> by `pages.yml`. Directory index
pages are `README.md` so GitHub renders them when browsing a folder; VitePress
rewrites them to site index pages, so one file serves both.

| Path | Purpose |
|---|---|
| `.vitepress/config.mts` | Site config: nav, sidebar, search. **Add new pages to the sidebar or they are unreachable** |
| `README.md` | Documentation index |
| `ai/README.md` | **AI session brief — the entry point for a new session** |
| `ai/repo-map.md` | This file |
| `glossary.md` | Portuguese fiscal and restaurant terminology |
| `architecture/README.md` | System overview: three tiers, ownership, shared kernel |
| `architecture/api-contract.md` | **Rules every endpoint obeys** so Android/iOS ship with no backend change |
| `architecture/money.md` | Why integer cents; allocation, rounding, formatting |
| `architecture/multi-tenancy.md` | Query filters + RLS; the system context |
| `architecture/module-boundaries.md` | The five rules modules obey |
| `architecture/site-agent.md` | In-restaurant process design (stub status) |
| `architecture/conventions.md` | Code conventions, build policy, suppression register |
| `architecture/decisions/README.md` | ADR index with one-line summaries |
| `architecture/decisions/0001..0010` | ADRs — each with a "Revisit when" trigger |
| `fiscal/README.md` | ATCUD, signature chain, QR, SAF-T, document types, VAT |
| `fiscal/certification.md` | AT process, prerequisites, what AT verifies |
| `fiscal/key-management.md` | Signing key custody and open questions |
| `development/getting-started.md` | Prerequisites, build, run, local infra |
| `development/testing.md` | The four-tier testing bar |
| `development/e2e-testing.md` | End-to-end strategy; Playwright recommendation (next session) |
| `development/documentation.md` | The documentation contract |
| `features/` | Per-feature documentation, one page each |
| `product/roadmap.md` | **Increments I0–I8 with demo scripts. What to build next** |
| `product/backlog.md` | 290 tasks, 20 epics, stable IDs. Task status |
| `product/differentiation.md` | Competitive positioning; DIF epic rationale |
| `product/plan.md` | Approved build plan and 6-month roadmap (historical) |
| `product/status.md` | **Honest inventory of which code exists** |

## Not yet created

| Path | Purpose | Increment |
|---|---|---|
| `web/ui` | Shared component library | I0/I1 |
| `web/sdk` | TypeScript client generated from OpenAPI | I0/I1 |
| `web/admin` | Back-office SPA | I1 |
| `web/kds` | Kitchen display | I4 |
| `web/order` | QR self-ordering | I8+ |

Also not yet created: the deployment target for OPS-11 (I0) — the one
remaining piece of I0.
