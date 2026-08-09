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
| `Program.cs` | ✅ | Two-role DI wiring (runtime vs. migration connection — [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md)), API versioning, CORS (`Cors:AllowedOrigins`, for web clients; `WithExposedHeaders("ETag", "X-Next-Cursor")` — API-10/API-09, neither is a CORS "simple" response header so browser JS can't read them without this), Brotli/gzip response compression incl. `application/problem+json` (API-11 — `UseResponseCompression()` must run first in the pipeline, before anything writes a response body), migration runner, dev seeding (Catalog + Floor), `ClientRequirements` config binding (API-07), full middleware pipeline incl. `ClientVersionMiddleware` (API-06, runs before request logging so enrichment reaches the request-completion line), `/health` liveness + `/health/ready` readiness (OPS-09) |
| `HealthChecks/DatabaseHealthCheck.cs` | ✅ | `IHealthCheck` running `SELECT 1` against the runtime (`brasa_app`) connection. Tagged `"ready"` — mapped only at `/health/ready`, never at the liveness `/health` — so PostgreSQL being briefly unreachable stops traffic routing but never triggers a process restart. OPS-09 |
| `Tenancy/DevTenantMiddleware.cs` | ✅ | Attributes every request to one hardcoded tenant. **The entire auth story until IDN-03…08 (I3).** Throws if `IsProduction()` |
| `Idempotency/IdempotencyMiddleware.cs` | ✅ | Requires `Idempotency-Key` on mutating `/api` requests; replays the cached response on repeat. In-memory, per-instance — durable store needed before scaling out. Verified by an automated replay harness, not just the doc comment (QA-11, `idempotency.spec.ts`) |
| `ClientVersioning/ClientInfo.cs` | ✅ | `TryParse` for the `X-Brasa-Client` header (`<client-id>/<version> (<platform>)`, API-06) — never throws, since a missing/malformed header is the expected case today (no client sends it yet) |
| `ClientVersioning/ClientVersionMiddleware.cs` | ✅ | Best-effort header parse; stores the result on `HttpContext.Items` for `GET /client-requirements` and pushes `ClientId`/`ClientVersion`/`ClientPlatform` into Serilog's `LogContext` so every log line for the request carries it. Never fails the request on a missing/malformed header |
| `ClientVersioning/ClientRequirementEntry.cs` | ✅ | Configuration-bound version policy for one client id, under `appsettings.json`'s `ClientRequirements` section (API-07) — not the wire DTO, see `Contracts/ClientDtos.cs` |
| `CursorPagination.cs` | ✅ | `Encode(DateTimeOffset)`/`TryDecode(string?, out DateTimeOffset)` (API-09) — opaque base64 bookmark token, applied so far only to `GET /orders` (the one genuinely unbounded collection today; `/menu` and `/floor` are both bounded by the restaurant's own size) |
| `ErrorMapping.cs` | ✅ | The only place `ErrorType` → HTTP status is decided |
| `ETagResults.cs` | ✅ | `OkWithETag<T>(HttpContext, T)` (API-10) — SHA256 strong `ETag` over the serialized body, `304` when `If-None-Match` already matches. **Must** serialize with the app's configured `IOptions<JsonOptions>` (camelCase), not `JsonSerializer`'s own default (PascalCase) — using the wrong one silently renames every field in the response; this broke `pos`'s menu screen once, see the API-10 entry in [status.md](../product/status.md). Deliberately not used by `GET /floor` — table state changes too continuously for caching to pay off |
| `Endpoints/CatalogEndpoints.cs` | ✅ | `GET /menu` (`ETagResults.OkWithETag`, API-10), `DELETE /menu/items/{id}` (CAT-18, soft delete), `PUT /menu/items/{id}/details` (CAT-02 — description + declared allergens, the 14-category EU-regulated set), `POST /menu/items/import` (CAT-17 — CSV only, rows import independently, `400 catalog.import_empty`/`catalog.import_invalid_header` for the two whole-file failure cases) |
| `Csv/CsvParser.cs` | ✅ | Hand-written RFC 4180 reader (CAT-17) — quoting, `""`-escaped quotes, embedded newlines, CRLF/LF, blank lines skipped without producing an empty row. 8 unit tests in `tests/Brasa.Api.IntegrationTests/CsvParserTests.cs` |
| `Endpoints/FloorEndpoints.cs` | ✅ | `GET /floor`, `POST /tables/{id}/clear` |
| `Endpoints/OrderEndpoints.cs` | ✅ | `POST /orders` (against a real `tableId`; also resolves + validates `selectedModifierIds` — `ResolveModifiers`), `POST /orders/takeaway` (ORD-20 — no Floor involvement at all), `GET /orders` (history/search — ORD-22, filters + capped `take` + cursor pagination via `X-Next-Cursor` — API-09), `GET /orders/{id}`, `POST /orders/{id}/lines`, `PUT /orders/{id}/lines/{lineId}/notes` (ORD-06), `POST /orders/{id}/lines/{lineId}/transfer` (ORD-13 — moves a line onto a different open order, pure Ordering, no Floor involvement), `POST /orders/{id}/merge` (ORD-14 — moves every line from a secondary order into the primary, marks the secondary `Merged`, frees its table via `Release()`), `POST /orders/{id}/transfer` (ORD-12 — order status checked before either table is touched, then both table mutations commit in one `FloorDbContext` save), `GET /orders/{id}/split`, `POST /orders/{id}/split/by-item` (ORD-16 — structured body, still a pure preview), `GET /orders/{id}/split/by-cover` (ORD-17 — reuses `Money.Allocate(ReadOnlySpan<int>)` directly), `GET /orders/{id}/pre-bill` (ORD-18/19 — never calls `IFiscalProvider`), `POST /orders/{id}/close`. Composes Catalog + Ordering + Floor + Fiscal — see the module-boundaries note below |
| `Endpoints/ClientEndpoints.cs` | ✅ | `GET /client-requirements` (API-07) — reads the `ClientInfo` `ClientVersionMiddleware` parsed off `HttpContext.Items`, looks it up in the config-bound `ClientRequirements` section, `400 client.header_required` if absent/malformed, `404 client.unknown_client_id` if the client id isn't configured |
| `Contracts/*.cs` | ✅ | `MoneyDto`, `MenuItemDto` (incl. `Description`/`Allergens`, CAT-02)/`MenuCategoryDto`/`ModifierGroupDto`/`ModifierDto`/`UpdateMenuItemDetailsRequest` (CAT-02), `RoomDto`/`TableDto`, `OrderDto` (incl. `IsTakeaway`, ORD-20)/`OrderLineDto` (incl. `Notes`, ORD-06)/`OrderLineModifierDto`/`SetLineNotesRequest`/`TransferOrderRequest` (ORD-12)/`TransferLineRequest`+`TransferLineResponse` (ORD-13)/`MergeOrdersRequest`+`MergeOrdersResponse` (ORD-14)/`SplitByItemRequest`+`SplitByItemResponse` (ORD-16)/`OpenTakeawayOrderRequest` (ORD-20), `OrderSummaryDto` (ORD-22 — lighter than `OrderDto`, no line detail), `FiscalDocumentDto`, `PreBillDto`/`VatBreakdownDto` (ORD-18/19 — deliberately shaped nothing like `FiscalDocumentDto`), `ClientRequirementsDto` (API-07), `ImportMenuItemsRequest`/`ImportMenuItemsRowError`/`ImportMenuItemsResponse` (CAT-17) + mappings |
| `Seed/DevCatalogSeeder.cs` | ✅ | Seeds a Portuguese demo menu spanning both VAT bands, plus two items with modifier groups (Frango na Brasa's required "Tamanho", Água's required "Tipo") and a couple with a description/declared allergens (Bacalhau à Brás, Pastel de Nata — CAT-02). Guarded the same way as the mock fiscal provider |
| `Seed/DevFloorSeeder.cs` | ✅ | Seeds 2 rooms / 16 tables (doubled from 8 once the E2E suite passed twenty tests and started occasionally exhausting the pool under back-to-back full runs — see e2e-testing.md). Same guard |
| `appsettings.json` | ✅ | **Two** connection strings — `Postgres` (runtime, `brasa_app`) and `PostgresMigrations` (`brasa`, superuser); `ClientRequirements` section keyed by client id (API-07) — `pos-web` is the only real entry, add a row when `kds`/`admin`/`order` start sending `X-Brasa-Client` |

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
| `src/components/TablePicker.tsx` | ✅ | WEB-05 — rooms/tables as a static grid (not `Table.PositionX/Y` — that's the future drag-and-drop editor, FLR-03), colour-coded by state, tap Free to open / tap Dirty to clear. Also hosts "Nova venda ao balcão" (ORD-20), which skips table selection entirely |
| `src/components/ModifierPicker.tsx` | ✅ | CAT-03/04 — shown when a tapped menu item has modifier groups; single-select renders as radio-like buttons, multi-select as toggles capped at `maxSelect`. Validity mirrors the server's own min/max check exactly |
| `src/components/PreBill.tsx` | ✅ | ORD-18/19 — "Ver conta" preview modal. Shaped nothing like `Receipt.tsx`: no document number, ATCUD or QR anywhere in its markup, a bold non-fiscal notice instead |
| `src/components/TransferTablePicker.tsx` | ✅ | ORD-12 — "Transferir mesa" modal listing only currently-`Free` tables; the floor snapshot is re-fetched right before it opens, but the API is still the final word on a race |
| `src/components/*.tsx` | ✅ | `MenuGrid` (renders `Description`/`Allergens` when declared, CAT-02, full contrast — never dimmed, same reasoning as QA-14), `OrderSummary` (incl. its own `OrderLineNotes` sub-component, ORD-06 — add/edit/clear a line's kitchen note inline), `Receipt`, `ErrorBanner`, `LanguageToggle` |
| `src/lib/money.ts` | ✅ | `Intl.NumberFormat('pt-PT', …)` — never formats `Money` by hand, and deliberately never follows the language toggle (see [ADR 0011](../architecture/decisions/0011-i18n.md)) |
| `src/i18n/i18n.ts` | ✅ | i18next config — pt default, en toggle (WEB-13) |
| `src/i18n/languageStorage.ts` | ✅ | `LanguageStore` interface + `cookieLanguageStore`; the seam a mobile client swaps for `AsyncStorage` |
| `src/i18n/resources/{pt,en}.ts` | ✅ | UI copy, incl. `floor.*`. Menu item names and money are **not** here — see the ADR |
| `.env.example` | ✅ | Documents `VITE_API_BASE_URL`; defaults to the API's `http` launch profile |

⬜ **Missing:** auth, offline (Dexie), everything else past I0/I1's first
slice — see the `WEB` epic in [backlog.md](../product/backlog.md).

## `src/web/e2e` ✅ I0 + I1 harness, plus a first slice of I2

Playwright + TypeScript, chromium only. `playwright.config.ts`'s `webServer`
starts both the API (`dotnet run --no-build`) and the `pos` dev server
itself — Docker (PostgreSQL) is the only thing it doesn't start. Verified
locally from both a warm state and a hard cold start, and **several
consecutive full runs** under real 2-worker parallelism — that repetition is
what caught the `Table.Occupy()` concurrency bug (see the trap in
[README.md](README.md)) and then proved the fix; 37/37 passing on a clean
run. At twenty-plus tests, back-to-back full runs with no pause between them
had started occasionally exhausting the original 8-table pool (QA-02's known
limitation showing up in practice, not a product bug); mitigated by doubling
`DevFloorSeeder` to 16 tables — see e2e-testing.md. See
[../development/e2e-testing.md](../development/e2e-testing.md) for the QA-01
decision record and what QA-04/06/07/08 are still blocked on.

| File | State | Contents |
|---|---|---|
| `tests/walking-skeleton.spec.ts` | ✅ | QA-05 — drives the real `pos` UI: pick a free table off the floor plan → ring up (incl. the modifier picker) → split preview → close → receipt → clear the table. Runs in Portuguese, the app's default |
| `tests/modifiers.spec.ts` | ✅ | CAT-03/04 — the modifier picker itself: required-group validation blocks "Add", Cancel adds nothing, price deltas sum correctly onto the line |
| `tests/pre-bill.spec.ts` | ✅ | ORD-18/19 — no fiscal fields anywhere in the wire shape, VAT-band reconciliation, byte-for-byte matching reprints, 400/409 guards, WCAG scan on the dialog |
| `tests/order-history.spec.ts` | ✅ | ORD-22 — `GET /orders` filtering by status/table, correct totals and line counts, invalid-filter 400s. Also API-09 — walks the full `X-Next-Cursor` chain and checks each of its own created orders appears exactly once, rather than asserting exact page sizes (that flaked under concurrent specs sharing the dev database — see the trap below); a malformed `cursor` 400s |
| `tests/line-notes.spec.ts` | ✅ | ORD-06 — set/overwrite/clear a line's kitchen note via the API, unknown-line/too-long/closed-order guards, and the inline editor in `pos` (add → edit → clear, re-opening starts from the saved value not a stale draft) |
| `tests/transfer-table.spec.ts` | ✅ | ORD-12 — old table frees and new table occupies in the same response cycle, the order's lines survive untouched, occupied-target/unknown-table/closed-order guards. UI case uses `transferToAnyFreeTable` (`support/ui.ts`), a second retry-on-409 loop layered on `openAnyFreeTable`'s own — needs `test.setTimeout(120_000)` since their worst cases can add up |
| `tests/transfer-line.spec.ts` | ✅ | ORD-13 — API-level only (no `pos` UI yet). A line moves onto a different order, both totals update and persist, and the guards (same order as destination, unknown line, unknown destination, either order closed) all fire |
| `tests/merge-orders.spec.ts` | ✅ | ORD-14 — API-level only (no `pos` UI yet). Every line moves into the primary order, the secondary ends up empty and `Merged` (persisted), its table frees straight to `Free`, and the guards (self-merge, unknown secondary, either side closed) all fire |
| `tests/split-by-item.spec.ts` | ✅ | ORD-16 — a two-unit line split 1-and-1 across two groups sums back to the cent, the order is unchanged afterward (re-fetched), and the guards (no groups, empty group, unknown line, over-allocation, partial allocation) all fire |
| `tests/split-by-cover.spec.ts` | ✅ | ORD-17 — 22.60 EUR / 5 covers split 2-and-3 → exactly 9.04/13.56, the order is unchanged afterward, and the guards (no cover groups, a zero-cover group, covers not summing to `CoverCount`) all fire |
| `tests/takeaway.spec.ts` | ✅ | ORD-20 — opens with the all-zero table id and a custom or defaulted ("Levantamento") label, closes and issues a real fiscal document exactly like dine-in, and the UI flow ("Nova venda ao balcão", no covers line shown) works end-to-end through a real receipt |
| `tests/menu-item-details.spec.ts` | ✅ | CAT-02 — set/persist/clear a description and allergen set, an unrecognised allergen name 400s, an unknown item 404s, and both render on a real menu button in full contrast (not dimmed) |
| `tests/menu-etag.spec.ts` | ✅ | API-10 — `GET /menu` returns `200` with an `ETag` on first pull, `304` with no body (and the same `ETag` restated) when it's echoed back as `If-None-Match`, and an unrecognised `If-None-Match` value still gets the full `200` body |
| `tests/idempotency.spec.ts` | ✅ | QA-11 — a mutating request replayed 3× with the same `Idempotency-Key` returns byte-identical responses and runs its side effect exactly once: `POST /orders` replayed never creates a second order for the table, `POST /orders/{id}/close` replayed never issues a second fiscal document. Plus the negative cases: a different key against the same now-occupied table is a genuine `409` (not a cache hit), and a missing key `400`s |
| `tests/client-requirements.spec.ts` | ✅ | API-06/07 — a known client id (`X-Brasa-Client: pos-web/0.0.0 (web)`) returns its configured policy; a missing header and a malformed one both `400` the same way (`client.header_required`); a well-formed header naming an unconfigured client id `404`s (`client.unknown_client_id`) |
| `tests/response-compression.spec.ts` | ✅ | API-11 — `GET /menu` comes back `br`-encoded when offered, falls back to `gzip`; a `400` `application/problem+json` error response is compressed too, not just success bodies |
| `tests/menu-import.spec.ts` | ✅ | CAT-17 — a 4-row CSV with 2 valid and 2 invalid rows creates exactly 2 items (confirmed on the real `GET /menu` afterward, not just the import receipt) and reports the other 2 by row number with the bad value named; an empty CSV and a header missing a required column both `400` |
| `tests/accessibility.spec.ts` | ✅ | QA-14 — axe-core against the table picker, ordering screen, modifier picker and receipt (WCAG 2.0/2.1 A+AA). Found 5 real `color-contrast` failures on its first run, all from dimming text via CSS `opacity` — see [status.md](../product/status.md#accessibility-first-scan-five-real-fixes) |
| `tests/support/ui.ts` | ✅ | `openAnyFreeTable` — retries against a different table on a 409, the UI-side counterpart to `openOrderOnAnyFreeTable` below. See the concurrency trap in [README.md](README.md) |
| `tests/split-preview.spec.ts` | ✅ | API-level (no browser); sweeps `Money.Allocate` across 1/2/3/5/7-way splits |
| `tests/language-toggle.spec.ts` | ✅ | WEB-13 — default language, the pt→en toggle, cookie attributes (`Path`, `SameSite`, not `httpOnly`) surviving a reload, and money staying `pt-PT` in English mode |
| `tests/support/api.ts` | ✅ | QA-03 test-data builders. Looks menu items and tables up **by name/state**, never by id (ids are UUIDv7, not stable across a fresh database). `closeOrderAndClearTable` returns a table to the free pool — only 8 are seeded and the dev database persists across runs. `openOrderOnAnyFreeTable` retries on a 409 — see the concurrency trap in [README.md](README.md) |

## `tests/`

| Project | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | `Primitives/MoneyTests.cs` — 17 tests, exhaustive allocation over ~12,000 combinations. `ErrorCodeRegistryTests.cs` (API-04) — text-scans every `Error.*(...)` call site under `src/` against `docs/architecture/error-codes.md`, no Docker needed. `Time/PortugueseTimeZoneTests.cs` — 14 tests: IANA ids resolve on this runtime, Azores stays 1h behind the mainland year-round, the rollover-hour boundary is inclusive, and the same instant can land on two different business days in different regions |
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
| `openapi/README.md` | What `v1.json` is, why `servers` is stripped, how to regenerate it (API-13) |
| `openapi/v1.json` | The `/api/v1` OpenAPI 3.1 document, committed so the API's shape is reviewable in a diff (API-13). Regenerated by hand — no CI drift check yet, that's API-14 |
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
