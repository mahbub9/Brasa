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
| `Persistence/TenantAwareDbContext.cs` | ✅ | Base `DbContext` every module extends. Applies query filters, stamps tenant/audit fields on save, guards against tenant reassignment and against hard-deleting an `ISoftDeletable` entity |
| `Persistence/RowLevelSecurity.cs` | ✅ | `EnableFor`/`DisableFor` — emits the real RLS policy **and** grants the runtime role access, in the same migration |
| `Persistence/TenantSessionInterceptor.cs` | ✅ | Sets the `brasa.tenant_id` Postgres session variable RLS policies read, per connection |
| `Persistence/ModelBuilderExtensions.cs` | ✅ | `ApplyTenantQueryFilters` (also excludes soft-deleted rows for any `ISoftDeletable` entity), `MapMoney`, `ApplyEntityConventions` (incl. the `ValueGeneratedNever` fix) |
| `Persistence/PersistenceServiceCollectionExtensions.cs` | ✅ | `AddBrasaTenancy()` — one call wires tenancy + the session interceptor for a host |
| `Tenancy/TenantContextAccessor.cs` | ✅ | `AsyncLocal`-backed singleton so EF's cached compiled model doesn't permanently capture one request's tenant |
| `Messaging/IntegrationEvent.cs` | ✅ | Event, handler and dispatcher contracts. **Only sanctioned cross-module channel** |
| `Messaging/OutboxMessage.cs` | ✅ | Transactional outbox row. Also the offline sync mechanism |

⬜ **Missing:** the dispatcher implementation.

## `src/backend/Brasa.Api` ✅ I0 + I1's first slice

| File | State | Contents |
|---|---|---|
| `Program.cs` | ✅ | Two-role DI wiring (runtime vs. migration connection — [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md)), API versioning, CORS (`Cors:AllowedOrigins`, for web clients), migration runner, dev seeding (Catalog + Floor), full middleware pipeline, `/health` liveness + `/health/ready` readiness (OPS-09) |
| `HealthChecks/DatabaseHealthCheck.cs` | ✅ | `IHealthCheck` running `SELECT 1` against the runtime (`brasa_app`) connection. Tagged `"ready"` — mapped only at `/health/ready`, never at the liveness `/health` — so PostgreSQL being briefly unreachable stops traffic routing but never triggers a process restart. OPS-09 |
| `Tenancy/DevTenantMiddleware.cs` | ✅ | Attributes every request to one hardcoded tenant. **The entire auth story until IDN-03…08 (I3).** Throws if `IsProduction()` |
| `Idempotency/IdempotencyMiddleware.cs` | ✅ | Requires `Idempotency-Key` on mutating `/api` requests; replays the cached response on repeat. In-memory, per-instance — durable store needed before scaling out |
| `ErrorMapping.cs` | ✅ | The only place `ErrorType` → HTTP status is decided |
| `Endpoints/CatalogEndpoints.cs` | ✅ | `GET /menu`, `DELETE /menu/items/{id}` (CAT-18, soft delete) |
| `Endpoints/FloorEndpoints.cs` | ✅ | `GET /floor`, `POST /tables/{id}/clear` |
| `Endpoints/OrderEndpoints.cs` | ✅ | `POST /orders` (against a real `tableId`; also resolves + validates `selectedModifierIds` — `ResolveModifiers`), `GET /orders/{id}`, `POST /orders/{id}/lines`, `GET /orders/{id}/split`, `GET /orders/{id}/pre-bill` (ORD-18/19 — never calls `IFiscalProvider`), `POST /orders/{id}/close`. Composes Catalog + Ordering + Floor + Fiscal — see the module-boundaries note below |
| `Contracts/*.cs` | ✅ | `MoneyDto`, `MenuItemDto`/`MenuCategoryDto`/`ModifierGroupDto`/`ModifierDto`, `RoomDto`/`TableDto`, `OrderDto`/`OrderLineDto`/`OrderLineModifierDto`, `FiscalDocumentDto`, `PreBillDto`/`VatBreakdownDto` (ORD-18/19 — deliberately shaped nothing like `FiscalDocumentDto`) + mappings |
| `Seed/DevCatalogSeeder.cs` | ✅ | Seeds a Portuguese demo menu spanning both VAT bands, plus two items with modifier groups (Frango na Brasa's required "Tamanho", Água's required "Tipo"). Guarded the same way as the mock fiscal provider |
| `Seed/DevFloorSeeder.cs` | ✅ | Seeds 2 rooms / 8 tables. Same guard |
| `appsettings.json` | ✅ | **Two** connection strings — `Postgres` (runtime, `brasa_app`) and `PostgresMigrations` (`brasa`, superuser) |

## `src/backend/Brasa.Modules.*`

| Module | State | Roadmap |
|---|---|---|
| `Identity` | 📁 empty | I3 |
| `Catalog` | ✅ `MenuCategory`, `MenuItem` (soft-deletable, CAT-18), `ModifierGroup`/`Modifier` (CAT-03/04, one item per group for now), `VatRate` (I0 placeholder for I1's full `TaxRule`), EF config + migrations + design-time factory | — |
| `Ordering` | ✅ `Order` aggregate (opens against a real `TableId`), `OrderLine`, `OrderLineModifier` (snapshotted selections), `SelectedModifier` (the record the API layer hands in — Ordering never resolves a modifier id itself), `OrderStatus`; EF config + migrations + design-time factory | — |
| `Floor` | ✅ `Room`, `Table` (`Free`/`Occupied`/`BillRequested`/`Dirty` state machine, `xmin` optimistic concurrency — see the trap in [README.md](README.md)); EF config + migrations (RLS) + design-time factory | — |
| `Fiscal` | ✅ `IFiscalProvider`, `FiscalDocument`, `FiscalDocumentLine` (VAT-inclusive derivation), `FiscalDocumentRequest`, `FiscalDocumentType` | — |
| `Payments` | 📁 empty | I6 |
| `Reporting` | 📁 empty | I8 |

Each references **only** `Brasa.Shared`. Never each other. `Order.TableId` is
a plain `Guid` reference to a Floor table, the same pattern
`OrderLine.MenuItemId` uses for a Catalog item — never a live query across
modules. `Brasa.Api`'s `OrderEndpoints` composes Catalog + Ordering + Floor +
Fiscal in one handler — that composition is the API layer's job, not
something the modules do to each other. See
[module-boundaries.md](../architecture/module-boundaries.md).

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

## `src/web/pos` ✅ I0 + I1's first slice

React 19 + Vite 8 + TypeScript. No auth, no offline — proves the API in a
browser. Hand-written API layer (`src/api/`) is a placeholder for `web/sdk`
(WEB-03, generated from OpenAPI) once a second client app needs it.

| File | State | Contents |
|---|---|---|
| `src/App.tsx` | ✅ | Orchestrates the phases: table picker (floor) → order (menu + summary) → receipt |
| `src/api/client.ts` | ✅ | `fetch` wrapper; `ApiError` carries the `ProblemDetails.code`; every mutation gets its own `Idempotency-Key` via `crypto.randomUUID()`; `getFloor`/`clearTable` |
| `src/api/types.ts` | ✅ | Hand-written mirror of `Brasa.Api/Contracts/*.cs` — kept in sync manually until WEB-03 |
| `src/components/TablePicker.tsx` | ✅ | WEB-05 — rooms/tables as a static grid (not `Table.PositionX/Y` — that's the future drag-and-drop editor, FLR-03), colour-coded by state, tap Free to open / tap Dirty to clear |
| `src/components/ModifierPicker.tsx` | ✅ | CAT-03/04 — shown when a tapped menu item has modifier groups; single-select renders as radio-like buttons, multi-select as toggles capped at `maxSelect`. Validity mirrors the server's own min/max check exactly |
| `src/components/PreBill.tsx` | ✅ | ORD-18/19 — "Ver conta" preview modal. Shaped nothing like `Receipt.tsx`: no document number, ATCUD or QR anywhere in its markup, a bold non-fiscal notice instead |
| `src/components/*.tsx` | ✅ | `MenuGrid`, `OrderSummary`, `Receipt`, `ErrorBanner`, `LanguageToggle` |
| `src/lib/money.ts` | ✅ | `Intl.NumberFormat('pt-PT', …)` — never formats `Money` by hand, and deliberately never follows the language toggle (see [ADR 0011](../architecture/decisions/0011-i18n.md)) |
| `src/i18n/i18n.ts` | ✅ | i18next config — pt default, en toggle (WEB-13) |
| `src/i18n/languageStorage.ts` | ✅ | `LanguageStore` interface + `cookieLanguageStore`; the seam a mobile client swaps for `AsyncStorage` |
| `src/i18n/resources/{pt,en}.ts` | ✅ | UI copy, incl. `floor.*`. Menu item names and money are **not** here — see the ADR |
| `.env.example` | ✅ | Documents `VITE_API_BASE_URL`; defaults to the API's `http` launch profile |

⬜ **Missing:** auth, offline (Dexie), everything else past I0/I1's first
slice — see the `WEB` epic in [backlog.md](../product/backlog.md).

## `src/web/e2e` ✅ I0 + I1 harness

Playwright + TypeScript, chromium only. `playwright.config.ts`'s `webServer`
starts both the API (`dotnet run --no-build`) and the `pos` dev server
itself — Docker (PostgreSQL) is the only thing it doesn't start. Verified
locally from both a warm state and a hard cold start, and **several
consecutive full runs** under real 2-worker parallelism — that repetition is
what caught the `Table.Occupy()` concurrency bug (see the trap in
[README.md](README.md)) and then proved the fix; 12/12 passing every time
since. See [../development/e2e-testing.md](../development/e2e-testing.md) for
the QA-01 decision record and what QA-04/06/07/08 are still blocked on.

| File | State | Contents |
|---|---|---|
| `tests/walking-skeleton.spec.ts` | ✅ | QA-05 — drives the real `pos` UI: pick a free table off the floor plan → ring up (incl. the modifier picker) → split preview → close → receipt → clear the table. Runs in Portuguese, the app's default |
| `tests/modifiers.spec.ts` | ✅ | CAT-03/04 — the modifier picker itself: required-group validation blocks "Add", Cancel adds nothing, price deltas sum correctly onto the line |
| `tests/accessibility.spec.ts` | ✅ | QA-14 — axe-core against the table picker, ordering screen, modifier picker and receipt (WCAG 2.0/2.1 A+AA). Found 5 real `color-contrast` failures on its first run, all from dimming text via CSS `opacity` — see [status.md](../product/status.md#accessibility-first-scan-five-real-fixes) |
| `tests/support/ui.ts` | ✅ | `openAnyFreeTable` — retries against a different table on a 409, the UI-side counterpart to `openOrderOnAnyFreeTable` below. See the concurrency trap in [README.md](README.md) |
| `tests/split-preview.spec.ts` | ✅ | API-level (no browser); sweeps `Money.Allocate` across 1/2/3/5/7-way splits |
| `tests/language-toggle.spec.ts` | ✅ | WEB-13 — default language, the pt→en toggle, cookie attributes (`Path`, `SameSite`, not `httpOnly`) surviving a reload, and money staying `pt-PT` in English mode |
| `tests/support/api.ts` | ✅ | QA-03 test-data builders. Looks menu items and tables up **by name/state**, never by id (ids are UUIDv7, not stable across a fresh database). `closeOrderAndClearTable` returns a table to the free pool — only 8 are seeded and the dev database persists across runs. `openOrderOnAnyFreeTable` retries on a 409 — see the concurrency trap in [README.md](README.md) |

## `tests/`

| Project | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | `Primitives/MoneyTests.cs` — 17 tests, exhaustive allocation over ~12,000 combinations. `ErrorCodeRegistryTests.cs` (API-04) — text-scans every `Error.*(...)` call site under `src/` against `docs/architecture/error-codes.md`, no Docker needed |
| `Brasa.Fiscal.Portugal.Tests` | ✅ | 13 tests: `FiscalDocumentLineTests` (gross→net VAT derivation, exhaustive per rate — the regression test for the I0 VAT bug), `MockFiscalProviderTests` (per-tenant sequential numbering, mock markers, mixed-rate reconciliation) |
| `Brasa.Api.IntegrationTests` | ✅ | `TenantIsolationReflectionTests` (DAT-11 — every module's built EF model, no DB) + `TenantIsolationIntegrationTests` (QA-09/10 — real Testcontainers Postgres, queries as `brasa_app` via raw SQL so a disabled RLS policy can't hide behind the EF convenience filter). `Mvc.Testing` referenced, not yet used — no HTTP-level integration test exists yet |

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
| `architecture/error-codes.md` | The `Error.Code` registry (API-04) — every code, its `ErrorType`/HTTP status, and what it means. Enforced by `ErrorCodeRegistryTests` |
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
| `product/backlog.md` | 291 tasks, 20 epics, stable IDs. Task status |
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
