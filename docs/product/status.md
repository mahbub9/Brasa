# Build status

> **Purpose:** a project scaffold makes empty things look finished. This page is
> the honest inventory of **which code exists**. Update it in the same commit
> that changes reality.
>
> For **what to build next and task-level progress**, see
> [backlog.md](backlog.md) — 291 tasks with stable IDs. This page is
> component-level; the backlog is task-level.

**Last updated:** 2026-08-10 · **Roadmap phase:** I0 complete except deployment (OPS-11); I1's opening slice (real rooms and tables, FLR) proven end-to-end

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
| `Brasa.Shared` — `Result`/`Error` | ✅ | Expected failures as values. `ResultTests`/`ErrorTests` — 20 tests, incl. the `Value`-on-failure exception naming the error code, `Match`, implicit conversion, `ToResult`. `ErrorMappingTests` (in `Brasa.Api.IntegrationTests`, since `ErrorMapping` lives in `Brasa.Api`) pins all 6 `ErrorType`→HTTP status mappings in one place (a 6th, `RateLimited`→429, joined the original 5 with API-12), not just indirectly through whichever status each endpoint's own tests happen to trigger |
| `Brasa.Shared` — tenancy | ✅ | `ITenantContext`, resolve-once-per-scope `TenantContext` |
| `Brasa.Shared` — time | ✅ | `IClock`, `PortugueseRegion`, business-day calculation. `PortugueseTimeZone` — 14 tests: IANA ids actually resolve on this runtime, Azores is 1h behind the mainland year-round, the rollover-hour boundary is inclusive, and the same UTC instant can land on two different business days in different regions (the exact scenario the type's own doc comment warns about) |
| `Brasa.Shared` — persistence base | ✅ | `Entity` (UUIDv7), `ITenantOwned`, `IAuditable`, `ISoftDeletable` |
| `Brasa.Shared` — outbox contracts | ✅ | Types defined; **no dispatcher implementation yet** |
| `Brasa.Api` | ✅ | `/api/v1/ping`, `/menu` (+ soft-delete, `/items/{id}/details` — CAT-02, `/items/import` — CAT-17, `/items/{id}/availability` — CAT-13, `/items/{id}/price` — CAT-19, `/categories/{id}/visibility` — CAT-01, `ETag`/`If-None-Match` caching — API-10), `/floor`, `/orders` (+`GET` search/history — ORD-22, cursor-paginated via `X-Next-Cursor` — API-09, `/takeaway` — ORD-20, `/lines`, `/lines/{id}/notes` — ORD-06, `/lines/{id}/transfer` — ORD-13, `/merge` — ORD-14, `/split`, `/split/by-item` — ORD-16, `/split/by-cover` — ORD-17, `/pre-bill`, `/transfer` — ORD-12, `/close`), `/tables/{id}/clear`, `/tables/{id}/request-bill` (FLR-04), `/client-requirements` (API-07), `/health` (liveness), `/health/ready` (PostgreSQL, OPS-09). Serilog, ProblemDetails, API versioning, idempotency, `X-Brasa-Client` parsing (API-06), RFC 8594 `Deprecation`/`Sunset` headers (API-08, a no-op today), rate limiting per `(tenant, client id)` on `/api/**` (API-12, `429` with `Retry-After`), Brotli/gzip response compression incl. error bodies (API-11), CORS for web clients (`Cors:AllowedOrigins`, `ETag`/`X-Next-Cursor` exposed for browser reads) |
| EF Core + PostgreSQL + RLS | ✅ | **Verified live**, not just asserted: `brasa_app` (unprivileged runtime role) sees zero rows with no tenant set or the wrong tenant set, and cannot run DDL. Re-verified against the new `floor` schema too. See [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| `Modules.Identity` | 📁 | I3 (auth) |
| `Modules.Catalog` | ✅ | `MenuCategory`, `MenuItem` (incl. optional `Description` and declared `Allergens` — CAT-02), seeded demo menu spanning both VAT bands, soft delete (CAT-18), modifier groups (CAT-03/04) |
| `Modules.Ordering` | ✅ | `Order` aggregate — open against a real `Table` (`TableId`) or as a takeaway with no table at all (ORD-20, `IsTakeaway`), add line with modifiers (price/VAT/modifier snapshot, ORD-05), per-line kitchen notes (ORD-06), split evenly, by item or by cover (ORD-15/16/17), pre-bill preview (ORD-18/19), transfer to a different table (ORD-12, converts a takeaway to dine-in), move a single line onto a different order (ORD-13) or merge two orders (ORD-14, `OrderStatus.Merged`), close, history/search (ORD-22) |
| `Modules.Floor` | ✅ | `Room`, `Table` — full `Free ⇄ Occupied ⇄ Dirty ⇄ Free` lifecycle plus `BillRequested` (FLR-04, `POST /tables/{id}/request-bill`), and `Release()` — `Occupied`/`BillRequested` straight back to `Free`, skipping `Dirty`, used only for table transfers (ORD-12). `xmin`-based optimistic concurrency on `Table` so two concurrent occupy attempts can't both win. Seeded: 2 rooms, 16 tables |
| `Modules.Fiscal` | ✅ | `IFiscalProvider`, `FiscalDocument`, VAT correctly derived from gross (menu prices are VAT-inclusive) |
| `Modules.Payments` | 📁 | I6 |
| `Modules.Reporting` | 📁 | I8 |
| `Fiscal.Portugal` | 📁 | **Nothing fiscal is implemented.** I7 |
| `Fiscal.Mock` | ✅ | Deterministic, `MOCK-`-prefixed output, production guard enforced at DI registration |

## Site Agent

| Component | State | Notes |
|---|---|---|
| `Brasa.SiteAgent` | 🚧 | Host starts and stops cleanly. Nothing else — Month 3 |
| Fiscal signing | ⬜ | |
| ESC/POS printing | ⬜ | |
| LAN REST + SignalR hub | ⬜ | |
| Cloud outbox sync | ⬜ | |

## Web clients

| Client | State | Notes |
|---|---|---|
| `pos` | ✅ I0/I1 shell | React 19 + Vite 8 + TS: floor table picker (WEB-05, incl. "Nova venda ao balcão" for a takeaway order with no table — ORD-20) → menu, showing description + allergen tags when declared (CAT-02), incl. a modifier picker for items with groups (CAT-03/04) → lines, each with an inline kitchen-note editor (ORD-06) → transfer to a different table (ORD-12) → split preview → pre-bill preview (ORD-18/19, clearly labelled *documento não fiscal*) or flag the table `BillRequested` for the floor plan (FLR-04, "Pedir conta" — distinct from previewing the bill) → close → receipt. pt-PT default / en toggle, cookie-persisted (ADR 0011), including seeded table labels ("Mesa 1" → "Table 1" — real staff here aren't all Portuguese speakers) and a language-appropriate takeaway default label. No auth, no offline, no Dexie yet — those are I2 (see [roadmap.md](roadmap.md)) |
| `kds` | ⬜ | |
| `admin` | 🚧 WEB-09 shell + WEB-10 menu editor | React 19 + Vite 8 + TS, same tooling as `pos`, on port 5174. "Visão geral"/"Overview": real category/item/room/table counts from `GET /menu/all` and `GET /floor`, so the shell is provably wired to the API and not a static mock. "Menu": toggle a category's visibility, 86/reprice/delete an item, bulk-import more via CAT-17's CSV pipeline — no "create item" form exists because no such endpoint does either, admin included. Floor plan/Staff nav entries stay labelled "Brevemente"/"Coming soon" — FLR-03 and WEB-11 build those. pt-PT default / en toggle (ADR 0011, shares `pos`'s `brasa.lang` cookie) with real English copy throughout — not all staff read Portuguese. No auth yet (depends on IDN) |
| `order` (QR self-ordering) | ⬜ | |

## Tests

| Suite | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | 51 passing, incl. exhaustive allocation check, the error-code registry test (API-04, now scanning six `Error.*` factories, not five — `RateLimited` joined in API-12), `PortugueseTimeZoneTests` (previously zero coverage on code CLAUDE.md itself flags as easy to get wrong), and `ResultTests`/`ErrorTests` (previously zero direct coverage on the hard-rule-5 types themselves) |
| `Brasa.Fiscal.Portugal.Tests` | ✅ | 13 passing: gross→net VAT derivation (exhaustive per rate), mock provider sequential numbering, mixed-rate reconciliation |
| `Brasa.Api.IntegrationTests` | ✅ | 31 tests: `TenantIsolationReflectionTests` (DAT-11, no DB) + `TenantIsolationIntegrationTests` (QA-09/10) — real disposable PostgreSQL via Testcontainers, zero rows with no/wrong tenant, own rows only with the right one, DDL refused (the automated version of the manual check that first caught [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md)) — plus `CsvParserTests` (CAT-17, no DB), `ErrorMappingTests` (no DB, pins all 6 `ErrorType`→HTTP status mappings directly, since `ErrorMapping.ToProblem()` is, by its own doc comment, "the only place `ErrorType` is translated to an HTTP status"), `ApiDeprecationMiddlewareTests` (API-08, no DB) and `ApiRateLimitingTests` (API-12, no DB — partition-key logic; the real 429/`Retry-After` behaviour was verified live instead, see below) |
| E2E (Playwright) | ✅ | `src/web/e2e` — 74 tests (incl. `admin-shell.spec.ts`/`admin-language-toggle.spec.ts`/`admin-menu-management.spec.ts`, WEB-09/10, and table-label/takeaway-default English assertions in `language-toggle.spec.ts`), all green across several consecutive full runs under real parallel load (2 workers) — that repetition is what surfaced and then proved the fix for the table-occupy race below (and, later, occasionally exhausted the original 8-table pool under back-to-back full runs — a QA-02 scaling limitation, mitigated by doubling the seeded pool to 16). That same repeated-run discipline is what caught the API-10 JSON-casing regression below before it reached a commit, and shaped the API-09 pagination test itself: a first version asserting exact page sizes flaked under concurrent specs sharing the dev database, fixed by walking the full cursor chain and asserting only what must hold regardless of noise from other tests. UI walking-skeleton through the real table picker (QA-05), the modifier picker (CAT-03/04), the pre-bill preview (ORD-18/19), per-line kitchen notes (ORD-06), table transfer (ORD-12), line transfer (ORD-13, API-level), order merge (ORD-14, API-level), split by item and by cover (ORD-16/17, API-level), takeaway orders (ORD-20), menu item description/allergens (CAT-02), menu bulk CSV import (CAT-17), request-bill floor-plan signal (FLR-04), 86-ing a menu item (CAT-13), menu item repricing incl. the past-order-lines-immune-to-a-reprice invariant (CAT-19), menu category visibility (CAT-01), menu `ETag`/304 caching (API-10, now retry-tolerant of legitimate concurrent catalog mutations from sibling specs), idempotency replay — a retried close never double-issues a fiscal document (QA-11), client version negotiation (API-06/07), order-history cursor pagination (API-09), response compression incl. error bodies (API-11), accessibility scans (QA-14), API-level split-math sweep (QA-03), order history/search (ORD-22), language toggle + cookie persistence (WEB-13). CI job written but **not yet run in CI**. See [../development/e2e-testing.md](../development/e2e-testing.md) |

## I0 demo — verified live, not just unit-tested

Run end-to-end against the real API and a real PostgreSQL container on
2026-08-09: open a table → add 2× Frango na Brasa (13%) + 2× Imperial (23%) →
preview an even 3-way split (7.54/7.53/7.53, summing to the cent) → close →
receive a fiscal document whose gross total matches the order total shown
throughout service, with net + VAT reconciling to the cent. Idempotency
verified by replaying an identical `POST /orders` and confirming, via direct
SQL, that only one row was created.

The same flow was then re-verified through the actual HTTP path the `pos` web
shell uses — CORS preflight, `Origin: http://localhost:5173`, and the
`Idempotency-Key` header on every mutating call — confirming the API's JSON
shapes match the shell's TypeScript types field-for-field and that a missing
`Idempotency-Key` returns the `ProblemDetails.code` shape the client parses.

> **Update (i18n):** `pos` now defaults to Portuguese with an English toggle,
> preference persisted in a cookie (never sent to the API) behind a storage
> interface written so a future mobile client swaps in `AsyncStorage` instead
> — see [ADR 0011](../architecture/decisions/0011-i18n.md). Money and the
> receipt's issued date deliberately stay `pt-PT` regardless of the toggle —
> fiscal formatting is not UI chrome. Server-sent error text is **not** yet
> localized (known gap, documented in the ADR, not hidden).

> **Update:** the browser gap noted above has since been closed. A Playwright
> harness (`src/web/e2e`) now drives the actual rendered `pos` UI in a real
> Chromium instance — click the table-open form, click real menu buttons,
> read the rendered total and split amounts, close, read the rendered
> receipt — and passes, verified twice: once against already-running dev
> servers, once from a hard cold start (both processes killed, Playwright's
> `webServer` config launching `dotnet run` and `npm run dev` itself). See
> [../development/e2e-testing.md](../development/e2e-testing.md). The one
> piece still unverified is the CI job itself — written, not yet exercised by
> an actual GitHub Actions run.

> **Update (pre-bill, ORD-18/19):** `GET /orders/{id}/pre-bill` gives a table
> its bill before paying without issuing anything — it reuses
> `FiscalDocumentLine`'s gross→net/VAT math purely as a calculator, never
> calls `IFiscalProvider`, and `PreBillDto` carries no document number, ATCUD
> or QR field at all, so it cannot be mistaken for an invoice on the wire.
> Verified live (`pre-bill.spec.ts`): two consecutive fetches against an
> unchanged order reproduce identical lines, VAT breakdown and total (the
> "reprint" requirement, ORD-19) because nothing is persisted between calls;
> it 400s on an empty order and 409s once the order is closed, reusing
> `order.empty`/`order.not_open`; and the modal it opens in `pos` passes the
> same WCAG A/AA scan as every other screen.

> **Update (order history, ORD-22):** `GET /orders` filters by `status`,
> `tableId`, `openedFrom`/`openedTo`, and a capped `take` (1–200, default
> 50), returning the lighter `OrderSummaryDto` rather than every line's full
> detail — a history list is read by the page, not the order. Verified live
> (`order-history.spec.ts`) against the shared, never-reset dev database
> (QA-02's known limitation): a just-opened order shows up under
> `status=Open` and disappears from it the moment it's closed, the same
> order then appears under `status=Closed` with the right total, and an
> unrecognised status or an out-of-range `take` both 400 with a stable
> error code rather than silently returning nothing.

> **Update (kitchen notes, ORD-06):** `PUT /orders/{id}/lines/{lineId}/notes`
> sets or clears a per-line free-text note after the line is already rung
> up — editing a line itself isn't built yet (ORD-03: add only until I2), so
> this is scoped narrowly to notes rather than general line editing. Staff/
> kitchen visibility only; it never touches the Fiscal module. `pos` gets an
> inline editor per line (add → edit → clear). Verified live
> (`line-notes.spec.ts`): set/overwrite/clear all round-trip correctly, an
> unknown line id 404s, a note over 300 characters 400s, and setting a note
> on an already-closed order 409s, all with stable error codes.

> **Update (table transfer, ORD-12):** `POST /orders/{id}/transfer` moves a
> party to a different table mid-service. Order status is checked before
> either table is touched — if the transfer can't happen, nothing about
> Floor changes either — and then both table mutations (freeing the old one
> via the new `Table.Release()`, occupying the new one via the existing
> `Occupy()`) live in the same `FloorDbContext`, so they commit atomically
> in one `SaveChangesAsync`, unlike `OpenOrderAsync`/`CloseOrderAsync` which
> must coordinate two separate `DbContext`s. `pos` gets a "Transferir mesa"
> button opening a picker of currently-`Free` tables. Verified live
> (`transfer-table.spec.ts`): the old table returns to `Free` and the new
> one becomes `Occupied` in the same response cycle, the order's line
> survives the move untouched, transferring onto an already-occupied table
> 409s (`floor.table_not_free`), an unknown table 404s
> (`floor.table_not_found`), and transferring a closed order 409s
> (`order.not_open`) — all reused error codes, no new ones needed. Chasing
> this test's own flakiness under back-to-back full-suite runs also
> surfaced a real, separate UI bug: `ErrorBanner` wasn't `position: fixed`
> or elevated above a modal's `z-index`, so an error while the transfer
> picker was open rendered completely hidden behind its backdrop. Fixed by
> giving `.error-banner` `position: fixed` and a higher `z-index` — general
> correctness, not specific to this one flow.

> **Update (line transfer, ORD-13):** `POST /orders/{id}/lines/{lineId}/transfer`
> moves a single line onto a different open order — a dish following a guest
> who joins another table, or splitting a large party across two tables.
> Pure Ordering, unlike ORD-12: no Floor table changes hands, since both
> tables stay exactly as occupied as they were. Both orders' status is
> checked before either is touched, so a rejection never leaves a line
> half-detached. No `pos` UI yet, deliberately — the screen only ever shows
> one open order at a time, and picking *another* currently-open order is a
> real product-design question (search by table? a list?) that shouldn't be
> answered by inventing a UI nobody asked for, the same scoping call already
> made for ORD-22. Verified live at the API level (`transfer-line.spec.ts`):
> the line moves and both orders' totals update correctly, it's persisted
> (re-fetched independently, not just trusted from the response), and the
> guards (same order as destination, unknown line, unknown destination,
> either order closed) all fire with the right codes.

> **Update (merge orders, ORD-14):** `POST /orders/{id}/merge` moves every
> line from a secondary open order into a primary one — two parties
> combining onto one table. Added a third `OrderStatus`, `Merged` — no
> migration needed, since `OrderConfiguration` already stores status as a
> string column. Distinct from `Closed` on purpose: a merged order never
> gets a fiscal document, a closed one always does. The secondary's table
> frees directly via `Release()`, the same as ORD-12's old table — nothing
> was billed there, so it never passes through `Dirty`. No `pos` UI yet,
> the same scoping call as ORD-13. Verified live (`merge-orders.spec.ts`):
> every line survives the move with the primary's total exactly reflecting
> both orders' original totals, the secondary ends up empty and `Merged`
> (persisted, re-fetched independently), and the guards (merging into
> itself, unknown secondary, either side already closed) all fire.

> **Update (split by item, ORD-16):** `POST /orders/{id}/split/by-item`
> previews a bill where each guest pays for specific items rather than an
> equal share — "Ana had the fish, Rui had the steak." A pure preview like
> the even split (`GET /orders/{id}/split`), it never mutates order state;
> it just needs a structured body (which line, how much of its quantity,
> goes to which guest), so unlike the even split it has to be a `POST`. No
> `Money.Allocate` remainder distribution is involved at all — every line's
> quantity is allocated across the groups exactly once, so each portion is
> an exact multiple of the line's own per-unit price by construction, not
> an approximation. Verified live (`split-by-item.spec.ts`): a two-unit
> line split 1-and-1 across two groups sums back to the line's own total to
> the cent, the order itself is completely unchanged afterward (re-fetched,
> not just trusted), and the guards (no groups, an empty group, an unknown
> line, over-allocating past a line's remaining quantity, and leaving a
> line's quantity partially unallocated) all fire.

> **Update (split by cover, ORD-17):** `GET /orders/{id}/split/by-cover?covers=2&covers=3`
> previews a bill split proportionally to how many covers are in each
> group — a table of 5 splitting 2-and-3, not necessarily evenly. Reuses
> `Money.Allocate(ReadOnlySpan<int>)` directly, the exact "split unevenly by
> covers" case that overload's own remarks already called out — no new
> allocation logic needed. The cover groups must sum to exactly the order's
> own `CoverCount`; a request that doesn't describes a different party than
> the one actually seated. Stays a `GET`, like the even split, since the
> input is just a list of integers. Verified live
> (`split-by-cover.spec.ts`): a 22.60 EUR order with 5 covers split 2-and-3
> yields exactly 9.04/13.56 (summing to the cent), the order is unchanged
> afterward, and the guards (no cover groups, a zero-cover group, covers
> that don't sum to the order's own cover count) all fire.

> **Update (takeaway, ORD-20):** `POST /orders/takeaway` rings up a
> counter-sale order with no Floor table involved at all — pure Ordering,
> unlike every other `Open*` path. `Order.IsTakeaway` is the real signal
> callers must check; `TableId` stays `Guid.Empty`, but that is deliberately
> never a magic value anywhere else in this codebase, and must not become
> one here either. `CoverCount` is fixed at 1 (meaningless for a counter
> sale, not worth a nullable column for). Transferring a takeaway order onto
> a real table (ORD-12) converts it to dine-in — the one place
> `IsTakeaway` can turn false again; nothing converts the other way.
> `pos` gets a "Nova venda ao balcão" entry point on the table picker that
> skips table selection entirely, and hides the covers line for takeaway
> orders (showing "1 pessoas" for a counter sale would be actively
> misleading). Verified live (`takeaway.spec.ts`): opens with the all-zero
> table id and a custom or defaulted ("Levantamento") label, closes and
> issues a real fiscal document exactly like a dine-in order (Close/Fiscal
> never look at Floor at all), and the UI flow works end-to-end through a
> real receipt.

> **Update (discounts, ORD-11):** `PUT /orders/{id}/lines/{lineId}/discount`
> and `PUT /orders/{id}/discount` set or clear a percentage or fixed-amount
> discount on one line or the whole order; both fields null clears it. They
> compose — an order-level discount applies on top of the subtotal after any
> line discounts, not instead of them. A fixed amount that would exceed the
> total it's applied to is rejected outright rather than silently clamped,
> so a mistyped value surfaces immediately instead of quietly comping an
> item. No manager-authorisation gate exists yet (IDN-11, once staff
> accounts and roles exist) — ships ahead of that trigger, the same shape as
> CAT-13's 86-ing and CAT-19's repricing. The part that needed real care:
> `CloseOrderAsync`'s own invariant, `order.Total` must equal
> `document.GrossTotal` to the cent, still has to hold with a discount
> applied. It does, by construction rather than convention — a shared
> `BuildFiscalLines` helper (now used by both the pre-bill preview and
> close, closing a small duplication that existed before this) renders each
> line's discount as its own negative `FiscalDocumentLine` at that line's
> own VAT rate, and prorates an order-level discount across lines by
> `Money.Allocate`, the same proportional-distribution tool `SplitByCover`
> already uses — so the shares it returns always sum back to exactly the
> discount taken off `order.Total`, with no remainder lost. Known,
> documented gap: `SplitByItem`'s by-item preview doesn't yet reflect a
> discount (it computes portions from a line's raw unit price, not
> `OrderLine.LineTotal`) — `SplitEvenly`/`SplitByCover` don't have this gap,
> since both inherit a discount automatically through `Order.Total`.
> Verified live (`discounts.spec.ts`): a line discount reducing only that
> line, an order discount reducing the post-line-discount subtotal further,
> clearing restoring the original total, every rejection path (unrecognised
> type string, percentage outside (0, 100], a fixed amount exceeding the
> total, only one of type/value given, an unknown line, a closed order), and
> — the fiscally load-bearing checks — the pre-bill's VAT breakdown still
> sums to `order.Total` and `document.GrossTotal` still equals `order.Total`
> to the cent once the order is actually closed with a discount applied.

> **Update (menu item details, CAT-02):** `PUT /menu/items/{id}/details`
> sets a menu item's description and declared allergens. Allergens are
> modelled as a closed `Allergen` enum over the 14 categories EU food-
> information law requires disclosing (Regulation (EU) No 1169/2011,
> Annex II) — a fixed, EU-wide taxonomy unchanged since 2014, and
> deliberately *not* treated with the same "needs an accountant to confirm"
> caution as `VatRate`, which really is a Portugal-specific figure in
> flux. Stored as a comma-joined string column (matching how every other
> enum in this codebase already converts to a string — `VatRate`,
> `TableState`, `OrderStatus` — rather than a provider-specific array
> column just for this one property) with a `ValueComparer` so EF Core's
> change tracking sees mutations correctly. `SetAllergens` replaces the
> full set rather than being additive, so correcting a wrongly-declared
> allergen is one call. `pos` renders both on the menu screen, in full
> `--ink` contrast rather than dimmed — allergen information is
> safety-relevant, the same reasoning QA-14 already established for never
> using CSS `opacity` for hierarchy. Verified live
> (`menu-item-details.spec.ts`): set/persist/clear both fields, an
> unrecognised allergen name 400s (`catalog.invalid_allergen`), an unknown
> item 404s, and the description and allergen tags render correctly on a
> real menu button.

> **Update (menu ETag, API-10):** `GET /menu` now returns a strong `ETag`
> (SHA256 over the serialized body) and answers a repeat pull with a
> bodyless `304` when the client's `If-None-Match` already matches —
> the menu changes rarely, so most POS syncs should not re-transfer the
> same JSON. Deliberately not applied to `GET /floor`: table state changes
> continuously through service, so almost every request would be a genuine
> cache miss. `Cors:AllowedOrigins` now exposes `ETag` too — it is not one
> of the CORS "simple" response headers, so without this a browser client
> could never read it to send back as `If-None-Match`. Verified live: first
> pull is `200` with an `ETag` header, a repeat with that value in
> `If-None-Match` is `304` with no body. A real bug surfaced by that live
> check, not by `dotnet build` or the unit suite: the helper computed the
> `ETag` and body by calling `JsonSerializer` with its own default options
> (PascalCase field names) instead of the app's configured ones (camelCase),
> which silently changed every field name `GET /menu` returned and broke
> `pos`'s menu screen (`category.items` came back `undefined`). Fixed by
> resolving `IOptions<JsonOptions>` from `HttpContext.RequestServices` —
> the same options `Results.Ok(...)` already uses — instead of the type
> default.

> **Update (idempotency replay harness, QA-11):** `IdempotencyMiddleware`
> (API-05) has carried the "a retried close must not double-issue a fiscal
> document" invariant since I0, stated only in its own doc comment and CLAUDE.md
> hard rule 3 — with no automated proof. `idempotency.spec.ts` now replays a
> mutating request 3× with the same `Idempotency-Key` and asserts the
> response is byte-identical (`Idempotent-Replay: true` on replays 2/3) *and*
> the underlying side effect ran exactly once: `POST /orders` replayed never
> creates a second order for the table (`GET /orders?tableId=…` still shows
> exactly one), and `POST /orders/{id}/close` replayed 3× returns the same
> `documentNumber`/`atcud` every time — the exact scenario the middleware's
> own comment names. Two negative cases guard against the test passing for
> the wrong reason: a *different* key against the same now-occupied table is
> a genuine `409`, not a cache hit (proves the cache is keyed correctly, not
> just returning the last response for anything); a missing key `400`s
> (`request.idempotency_key_required`).

> **Update (client version negotiation, API-06/07):**
> `docs/architecture/api-contract.md` §3 exists because "a web client
> updates on refresh, a mobile app does not" — old app versions persist for
> months, so the backend has to assume it's always serving clients it can't
> upgrade. `ClientVersionMiddleware` parses the `X-Brasa-Client` header
> (`<client-id>/<version> (<platform>)`) on every request — best-effort,
> since no client sends it yet, so a missing/malformed value never fails
> the request, it just skips enrichment. When present, it's stashed on
> `HttpContext.Items` for endpoints to read, and pushed into Serilog's
> `LogContext` so `ClientId`/`ClientVersion`/`ClientPlatform` show up on
> every log line for that request, including the request-completion
> summary — verified directly in the console/Seq output, not just asserted
> by the type shape. `GET /client-requirements` reads that parsed header
> and looks up the caller's client id in a config-bound `ClientRequirements`
> section (no admin UI to edit this yet, so configuration rather than a
> database table), returning `{minimumSupported, recommended, sunsetAfter}`
> so an app can decide for itself whether to show "update required".
> Ships ahead of any client that actually sends the header or calls the
> endpoint, the same way CAT-02/CAT-18 shipped ahead of the admin UI that
> will eventually call them. Verified live, all four cases: known client id
> → `200` with its policy; missing header → `400 client.header_required`;
> malformed header → the same `400` (parse failure is indistinguishable
> from absence, deliberately); well-formed header naming an unconfigured
> client id → `404 client.unknown_client_id`.

> **Update (cursor pagination, API-09):** `GET /orders` shipped with ORD-22
> as a flat capped `take` (1–200, default 50) — fine for a page one, but
> with no way to reach a page two. `api-contract.md`'s own mobile-readiness
> rule ("cursor pagination on every collection, no unbounded lists ever")
> already called this out. `CursorPagination` encodes a single sortable
> bookmark (the last row's `OpenedAtUtc`) as an opaque base64 token; a
> client sends it back verbatim as `cursor`, never constructs one itself.
> Applied only to `GET /orders` — the one collection here that's genuinely
> unbounded over a restaurant's lifetime; `/menu` and `/floor` are both
> bounded by the restaurant's own size and don't need it. Deliberately
> additive rather than a breaking change to an already-shipped `/api/v1`
> endpoint: the response body is still the same bare `OrderSummaryDto[]`,
> and the next page's bookmark rides on a new `X-Next-Cursor` response
> header instead, present only when the page came back full (the only case
> where there might be more). Verified live: a full page returns the
> header; fetching again with that value returns older, non-overlapping
> rows; a malformed `cursor` `400`s (`order.invalid_cursor`) rather than
> silently falling back to page one.

> **Update (response compression, API-11):** `api-contract.md` §9 says
> "compression on all responses" — assume a phone on cellular in a
> basement dining room. Brotli and gzip are both registered, with Brotli
> preferred when the client offers it; `EnableForHttps = true` is safe
> here specifically because the BREACH-attack precondition (a compressed
> response mixing a secret with attacker-influenced content, classically a
> CSRF token reflected next to a query parameter in server-rendered HTML)
> doesn't apply to this API — bearer-token auth with no cookies
> ([ADR 0008](../architecture/decisions/0008-token-auth-no-cookies.md)),
> and every response body comes straight from the database, never
> echoing caller-supplied content back into the same body as a secret.
> `application/problem+json` was added to the default MIME type list —
> ASP.NET Core's own default list compresses `application/json` but not
> the RFC 9457 error content type this API actually uses, so error
> responses would otherwise have silently gone uncompressed. Verified
> live: `br` when offered, falls back to `gzip` when it isn't, genuinely
> uncompressed when the client sends no `Accept-Encoding` at all, and
> confirmed compression and `ETag`'s `304` path (API-10) don't interfere
> with each other.

> **Update (OpenAPI document, API-13):** [docs/openapi/v1.json](../openapi/v1.json)
> — the `/api/v1` OpenAPI 3.1 document, fetched from the already-wired
> `Microsoft.AspNetCore.OpenApi` dev endpoint (`GET /openapi/v1.json`) and
> committed so the API's public shape is reviewable in a diff, not only
> inspectable by running the app. The `servers` array is stripped before
> committing — it's filled in from whichever host generated the document
> (`localhost:<port>` locally), so keeping it would make every regeneration
> a noisy diff implying a URL the document doesn't actually promise. No CI
> enforcement that it's current yet; that's API-14, a separate, deliberately
> not-yet-built task — see [docs/openapi/README.md](../openapi/README.md)
> for the manual regeneration steps in the meantime.

> **Update (menu bulk import, CAT-17):** `POST /menu/items/import` accepts
> a CSV file body (`CategoryName,Name,Description,Price,VatRate,IsAlcoholic`
> header, `Description`/`IsAlcoholic` optional) and creates a menu item per
> valid row — the same "ships ahead of any UI that will call it" pattern as
> CAT-02/CAT-18, since there is still no `admin` app to build a file-upload
> screen in. Parsed by a hand-written RFC 4180 reader (`CsvParser`) rather
> than a new dependency — quoting, `""`-escaped quotes, embedded newlines
> and both CRLF/LF line endings are all it needs to get right, and 8 unit
> tests (`CsvParserTests`) pin each of those cases directly rather than only
> through the import endpoint's E2E coverage. Rows import independently: an
> unknown category or an unparsable price is reported per-row (1-indexed
> against the data rows, matching what a user sees in a spreadsheet) and
> skipped, it doesn't fail the whole file — create-only, not upsert, so
> importing the same file twice creates duplicates on purpose rather than
> silently guessing at a merge. Verified live: a 4-row file with 2 valid and
> 2 invalid rows came back `{"created":2,"errors":[...]}` with the exact bad
> value named in each row's message, and the 2 valid rows were confirmed on
> the real `GET /menu` response afterward, not just in the import receipt.

> **Update (`PortugueseTimeZone` test coverage):** `Brasa.Shared.Time`'s
> region/business-day logic had zero tests despite CLAUDE.md itself flagging
> it as easy to get wrong ("the Azores are an hour behind the mainland,
> which affects daily close and SAF-T period boundaries"). `PortugueseTimeZoneTests`
> (14 tests) closes that gap: every region's IANA id actually resolves via
> `TimeZoneInfo.FindSystemTimeZoneById` on this runtime (the kind of thing
> that fails at first use in a new environment, not at compile time, if the
> OS/ICU data disagrees), Azores stays exactly one hour behind the mainland
> year-round (both observe the same EU DST transitions, so the differential
> never moves), the rollover hour itself belongs to the new business day
> (inclusive boundary), a sale just after midnight rolls back across a
> month boundary correctly, and — the scenario the type's own doc comment
> warns about — the same UTC instant can land on two different business
> days in Continental vs. Azores. One of these tests initially had its own
> UTC-to-local arithmetic wrong in the setup (an off-by-one day), not a bug
> in `PortugueseTimeZone` itself — caught by the test actually failing on
> first run, not assumed passing.

> **Update (`Result`/`Error`/`ErrorMapping` test coverage):** these three
> types are how hard rule 5 ("expected failures return `Result`, not
> exceptions") and hard rule 11 ("error codes are a public contract") are
> actually implemented, and none of them had a direct unit test — only
> indirect exercise through whichever specific status code each endpoint's
> own E2E test happened to assert. `ResultTests`/`ErrorTests` (20 tests)
> pin `Result<T>.Value` throwing `InvalidOperationException` with the error
> code named in the message on a failure, `Match` invoking the right
> branch, the implicit `T → Result<T>` conversion, and each `Error` factory
> setting the right `ErrorType`. `ErrorMappingTests` (3 tests, in
> `Brasa.Api.IntegrationTests` since `ErrorMapping` lives in `Brasa.Api`)
> pins all 5 `ErrorType → HTTP status` mappings in one place — `Validation`
> → 400, `Forbidden` → 403, `NotFound` → 404, `Conflict` → 409, `Failure` →
> 500 — plus that `Error.Code` travels as a `code` extension by exactly
> that name, since that's the literal field mobile clients branch on.

> **Update (request bill, FLR-04):** `Table.RequestBill()` has existed as a
> domain transition since the floor lifecycle first shipped, and `pos`
> already had the CSS (`.floor-table-BillRequested`) and i18n strings
> (`floor.state.BillRequested`) to render it — but nothing ever called it,
> because no endpoint existed. `POST /tables/{id}/request-bill` (mirroring
> `POST /tables/{id}/clear` exactly) and a new "Pedir conta" button in
> `OrderSummary` close that gap. Deliberately a separate action from the
> pre-bill preview (ORD-18/19, "Ver conta"): that stays a read-only `GET`
> with no side effect, since REST safety matters more than convenience
> here, so a client re-fetching the pre-bill can never accidentally flag
> the table. Hidden for takeaway orders (`!order.isTakeaway`) — there is no
> physical table to flag when `TableId` is the `Guid.Empty` sentinel.
> Verified live in a real Chromium browser via Playwright, not just against
> the API directly: opening a table, ringing up an item, and clicking
> "Pedir conta" flags it `BillRequested` on the very next `GET /floor`: a
> free table 409s (`floor.table_not_occupied`, the same code `MarkDirty`/
> `Release` already use) and an unknown table 404s.

> **Update (86-ing, CAT-13):** the same "domain and guard already existed,
> nothing could ever reach them" pattern as FLR-04, found by the same sweep
> for methods with no caller. `MenuItem.MarkAvailable`/`MarkUnavailable`
> have existed since I0, and `AddLine` has enforced `IsAvailable` for just
> as long (`catalog.item_unavailable`) — but no endpoint ever set
> `IsAvailable` to anything but its default `true`, so that guard was dead
> code that could never actually trigger outside a test that constructed
> the state by hand. `PUT /menu/items/{id}/availability` closes the loop.
> Ships ahead of any UI that will call it — no admin back-office, no
> in-order 86 control in `pos` — the same as CAT-02/CAT-17/CAT-18. Verified
> live end to end, not just that the flag flips: 86'd item disappears from
> `GET /menu`, a real `POST /orders/{id}/lines` against it now genuinely
> 409s with `catalog.item_unavailable` instead of that path being
> theoretical, un-86'ing restores both, and an unknown item id 404s.

> **Update (menu item price editing, CAT-19, newly minted):** a third
> instance of the exact same shape as FLR-04 and CAT-13, found by the same
> sweep. `MenuItem.Reprice` has had its own negative-price guard since I0;
> nothing ever called it, so it was reachable only from a unit test that
> constructed the state directly. `PUT /menu/items/{id}/price` closes the
> loop — ships ahead of any UI, same as CAT-02/13/17/18. The one thing this
> gap needed that the other two didn't: proving the *existing* safety
> mechanism actually holds under a real reprice, not just that the new
> endpoint works. `OrderLine.UnitPrice` has snapshotted at add-time since
> I0 (`MenuItem.Price`'s own doc comment: "changing a price here never
> rewrites history"), so past orders were already safe by construction —
> but that had never been exercised against a *live* repricing, only
> asserted by the type shape. Verified live: rang up a line at 3.50,
> repriced the item to 5.00, and the already-open order's line total was
> still 3.50 while `GET /menu` showed 5.00 — confirming the invariant holds
> under the real code path, not just in theory. Negative price and unknown
> item both rejected (`catalog.invalid_price`/`catalog.item_not_found`).

> **Update (menu category visibility, CAT-01):** a fourth instance of the
> FLR-04/CAT-13/CAT-19 shape, one level up — a whole category, not a
> single item. `MenuCategory.IsVisible` has gated `GET /menu`'s query
> since I0 (`.Where(c => c.IsVisible)`), but the class had no setter for
> it at all — not even an unreachable one, unlike the other three cases.
> CAT-01's own backlog title names "visibility" as in scope and the row
> was already marked done, which made this the clearest case yet of a
> claimed-done feature that had never actually been reachable.
> `PUT /menu/categories/{id}/visibility` (`MarkHidden`/`MarkVisible`,
> mirroring `MenuItem`'s `MarkAvailable`/`MarkUnavailable` pattern exactly)
> closes it — hiding a category removes it *and every item under it* from
> `GET /menu` in one call, verified live against the real seeded menu:
> hide "Sobremesas" → both it and its two items vanish; show → both
> restored; unknown category id → `404 catalog.category_not_found`.
>
> This one also surfaced a genuine test-suite gap, not a product bug:
> `menu-etag.spec.ts` assumed nothing mutates `GET /menu` between its own
> two back-to-back calls, which was true when it was written but stopped
> being true once CAT-01/13/19 all landed sibling specs that legitimately
> change the menu's content (and therefore its ETag) as part of what
> *they're* testing. Under real parallel workers one of those can land in
> the gap and turn the expected `304` into a genuine `200` — the ETag
> mechanism working correctly on content that actually changed, not a
> broken cache. Fixed by retrying the whole round trip (fresh `ETag`,
> immediate reuse) up to 5 times rather than weakening the assertion,
> matching the API-09 pagination test's own precedent for handling
> legitimate cross-spec interference under `fullyParallel`.

Three real bugs were found and fixed by this live run — none were caught by
`dotnet build` or the pre-existing unit tests:

1. **RLS was a complete no-op.** The bootstrap Postgres role is a superuser,
   and superusers bypass row-level security unconditionally regardless of
   `FORCE ROW LEVEL SECURITY`. Fixed by splitting into an unprivileged runtime
   role and a migration-only owner role. See
   [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md).
2. **New order lines were tracked `Modified` instead of `Added`.** EF Core's
   default `Guid` key convention assumes a non-default key on a
   navigation-discovered entity means it already exists. Fixed with
   `ValueGeneratedNever()` — see the comment on `ApplyEntityConventions` in
   `Brasa.Shared/Persistence/ModelBuilderExtensions.cs`.
3. **VAT was computed backwards.** Portuguese menu prices are VAT-inclusive;
   the fiscal document must derive net/VAT from the gross price, not add VAT
   on top. See [docs/fiscal/README.md](../fiscal/README.md#menu-prices-are-vat-inclusive).

## I1 slice — real tables, verified live

The first piece of I1 (`docs/product/roadmap.md`) landed the same way I0 did:
`Order.TableId` now references a real `Modules.Floor` table instead of a
free-text label, composed at the API layer exactly like Catalog and Fiscal
already were (`docs/architecture/module-boundaries.md`). Verified against the
real database, not just built:

- **The full table lifecycle**, end to end through the actual HTTP calls
  `pos` makes: `Free` → `Occupy` (open an order) → conflict (409) on a second
  attempt against the same table → `Dirty` (close) → `Free` (clear).
- **RLS on the new `floor` schema**, the same three-way check as ADR 0010: as
  `brasa_app`, zero rows with no tenant or the wrong tenant set, all rows
  with the right one.
- **The two-DbContext trade-off in `OpenOrderAsync`/`CloseOrderAsync`.**
  Ordering and Floor are separate schemas and separate `DbContext`s — opening
  a table is not one atomic transaction across them. `OpenOrderAsync` saves
  Floor first, `CloseOrderAsync` saves Ordering first — deliberately
  different orderings, because the failure each is protecting against is
  different. See the comments at each call site and
  `docs/architecture/module-boundaries.md` rule 5. Real cross-module
  atomicity is outbox-based work for I5+.
- **The E2E suite's own repeatability.** 16 tables are seeded (doubled from 8
  once the suite passed twenty tests and back-to-back full runs started
  occasionally exhausting the smaller pool) and the dev database isn't reset
  between runs — every spec that opens one now closes and clears it.
  Confirmed by running the full suite repeatedly with all tests green.

### A real concurrency bug, found by running the suite enough times

`Table.Occupy()` was a check-then-act on in-memory state with nothing at the
database level stopping two concurrent requests from both reading a table as
`Free`, both transitioning it in memory, and both successfully saving — EF's
default `SaveChangesAsync` is a blind `UPDATE ... WHERE Id = @id`, so the
second writer doesn't fail, it just silently overwrites the first. This
surfaced as E2E flakiness under Playwright's 2-worker parallelism: two
sub-tests would occasionally both "win" the same table, and later one of
them would fail trying to clear a table that turned out not to be `Dirty`
after all — because the *other* test had already cleared it first.

**Fixed** with an `xmin`-based optimistic concurrency token on `Table`
(`TableConfiguration.cs`) — `xmin` is a PostgreSQL system column every row
already has, so this needed no real schema change, just a metadata-only
migration (the scaffolded one had to be hand-edited to remove an `ADD COLUMN
xmin`, which Postgres rejects outright: the name is reserved). EF now adds
`AND xmin = @original` to the `UPDATE`'s `WHERE` clause, so the loser of a
race matches zero rows and throws `DbUpdateConcurrencyException` instead of
overwriting the winner — caught at each call site
(`OrderEndpoints.OpenOrderAsync`/`CloseOrderAsync`, `FloorEndpoints.ClearTableAsync`)
and turned into the same `floor.table_not_free` / `floor.table_not_dirty` 409
a non-concurrent conflict already returns. `OpenOrderAsync` was also
reordered to save Floor *before* Ordering specifically so this new check runs
before an `Order` row is ever created — a lost race now leaves nothing
behind to clean up.

Re-verified: the full E2E suite green across 4 consecutive runs under real
2-worker parallelism after the fix, versus reproducible failures before it —
this is the second bug this session where deliberately running something for
real, repeatedly, under realistic conditions caught what a single passing
run could not (the first was RLS, ADR 0010).

## Accessibility — first scan, five real fixes

`accessibility.spec.ts` (QA-14) runs axe-core against the table picker, the
ordering screen, the modifier picker modal, and the receipt — WCAG 2.0/2.1 A
and AA rules. First run failed both tests with five genuine
`color-contrast` violations, all from the same mistake: dimming text via CSS
`opacity` against a colored background, which blends toward that background
and quietly drops the effective contrast ratio below what the raw foreground
color alone would suggest.

Fixed by picking colors that clear 4.5:1 outright instead of relying on
opacity for visual hierarchy: `--muted` (used broadly — menu prices, empty
states, hints) went from 4.01:1 to 6.2:1; the header tagline and inactive
language-toggle button went from opacity-dimmed white (4.12:1 / 3.27:1) to
full-opacity white (5.1:1); the Free and "bill requested" floor-table state
badges went from 3.21:1 / 3.30:1 to 5.9:1 / 4.6:1. None of these were visibly
"broken" — a sighted reviewer skimming the UI would not have flagged any of
them — which is exactly why this needs a tool, not a glance, and why it's
now a permanent part of the suite rather than a one-off audit.

Scope: `pos` only. There is no guest-facing UI yet to check (`order`/QR
self-ordering is post-I8) — see the `QR` epic in
[backlog.md](backlog.md#qr--qr-self-ordering) for when that arrives and
needs the same treatment.

## Error codes are now a mechanically enforced contract

Hard rule 11 (`docs/ai/README.md`) always said `Error.Code` must never
change meaning once released, but nothing checked that beyond a comment and
good intentions. `docs/architecture/error-codes.md` is now the checked-in
registry — every code, its `ErrorType` (which decides the HTTP status), and
what triggers it — and `ErrorCodeRegistryTests` (API-04,
`tests/Brasa.Shared.Tests`) scans every `Error.Validation/NotFound/Conflict/
Forbidden/Failure(...)` call site under `src/` via a plain text-matching
regex and fails on any disagreement: a code removed or renamed, a new code
nobody documented, or — the actual meaning-change case — a code whose
`ErrorType` changed, silently changing the HTTP status a client sees for the
same string.

Verified the same way the other regression tests this session were: broke it
on purpose (renamed `order.already_closed` in source, left the registry
alone) and confirmed the test fails with the code named in the message,
before reverting. No Docker, no database — pure text scanning, ~0.2s.

## Mobile readiness

Android and iOS apps follow shortly after the web launch and must need **no
backend change**. These are the seams that make that true — all design-only so
far. Rules: [../architecture/api-contract.md](../architecture/api-contract.md).

| # | Seam | State | Retrofit cost if skipped |
|---|---|---|---|
| 1 | Token auth (OAuth 2.1 + PKCE), device-bound rotating refresh | ⬜ | **Highest** — tearing out cookie auth, re-authenticating every client |
| 2 | Device registry + terminal pairing | ⬜ | High — push tokens, revocation and terminal identity all hang off it |
| 3 | Cursor-based sync endpoints | ⬜ | High — changing the protocol breaks every offline client at once |
| 4 | Idempotency keys on all mutations | ✅ | API-05 — every mutating `/api` request; QA-11 automates the replay guarantee (a retried close never double-issues a fiscal document) |
| 5 | `X-Brasa-Client` header + `client-requirements` endpoint | ✅ | API-06/07 — best-effort header parsing, config-bound per-client-id policy. Ships ahead of any client sending it or calling the endpoint |
| 6 | Stable error-code contract | ✅ | API-04 — `Error.Code` exists **and** the stability guarantee is mechanically enforced: `ErrorCodeRegistryTests` fails the build if source and [error-codes.md](../architecture/error-codes.md) ever disagree |
| 7 | Push token registration + `IPushChannel` | ⬜ | Low — a migration and an endpoint |
| 8 | OpenAPI generation + breaking-change CI | 🚧 | Generation (API-13) is done — [docs/openapi/v1.json](../openapi/v1.json), regenerated by hand each time an endpoint changes. Breaking-change CI (API-14) is not — nothing yet fails the build on an unreviewed contract break |
| 9 | `/.well-known/` deep-link verification documents | ✅ | API-18 — honestly empty (`apps`/`details`/top-level arrays), not fabricated: no bundle id, Team id or package name exists to put in either file until a native app does |

Planned clients: staff handheld, owner dashboard, customer app, native KDS.
Stack undecided — the API stays client-agnostic so any of React Native, Flutter
or native Kotlin/Swift remains open.

## Infrastructure

| Component | State | Notes |
|---|---|---|
| Docker | ✅ | 29.6.2, Compose v5.3.1 |
| PostgreSQL | ✅ | 18.4 container, ICU locale provider, `pt-PT` |
| Seq (log viewer) | ✅ | `http://localhost:5341` — every log line during a request now carries `TenantId` (OPS-07, `TenantLoggingMiddleware`), verified by reading a real request's events straight from the console/Seq sink |
| CI (GitHub Actions) | ✅ | Build gate, tests, vulnerability scan, docs link check. `e2e` job added this session — written and locally-equivalent to what passed on this machine, but not yet exercised by an actual CI run |

## Blockers

| # | Blocker | Impact | Owner |
|---|---|---|---|
| 1 | Portuguese legal entity not yet formed | Cannot submit Modelo 24 to AT. **Start now — it is on the critical path for revenue, not for code** | Founder |
| 2 | VAT rules unconfirmed by an accountant | `TaxRule` design absorbs any answer, but rates must be verified before launch | Founder |
| 3 | `Brasa` trademark and domains not cleared | Check INPI (PT), EUIPO (classes 9/42), and `.pt`/`.com` before spending on branding | Founder |
