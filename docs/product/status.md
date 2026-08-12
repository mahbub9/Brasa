# Build status

> **Purpose:** a project scaffold makes empty things look finished. This page is
> the honest inventory of **which code exists**. Update it in the same commit
> that changes reality.
>
> For **what to build next and task-level progress**, see
> [backlog.md](backlog.md) — 291 tasks with stable IDs. This page is
> component-level; the backlog is task-level.

**Last updated:** 2026-08-12 · **Roadmap phase:** I0 complete except deployment (OPS-11); I1's opening slice (real rooms and tables, FLR) proven end-to-end

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
| `Brasa.Api` | ✅ | `/api/v1/ping`, `/menu` (+ soft-delete, `/items/{id}/details` — CAT-02, `/items/import` — CAT-17, `/items/{id}/availability` — CAT-13, `/items/{id}/price` — CAT-19, `/categories/{id}/visibility` — CAT-01, `ETag`/`If-None-Match` caching — API-10), `/floor`, `/orders` (+`GET` search/history — ORD-22, cursor-paginated via `X-Next-Cursor` — API-09, `/takeaway` — ORD-20, `/lines`, `/lines/{id}/notes` — ORD-06, `/lines/{id}/transfer` — ORD-13, `/merge` — ORD-14, `/split`, `/split/by-item` — ORD-16, `/split/by-cover` — ORD-17, `/pre-bill`, `/transfer` — ORD-12, `/close`), `/tables/{id}/clear`, `/tables/{id}/request-bill` (FLR-04), `/table-groups`, `/table-groups/{id}` (FLR-05), `/organizations`, `/organizations/{id}/sites`, `/sites/{id}/terminals` (IDN-01, create + list only), `/price-lists`, `/sites/{id}/price-lists`, `/price-lists/{id}/entries`, `/price-lists/{id}/effective-price/{menuItemId}` (CAT-05, create/read/add-entry + resolution, not yet wired into ordering), `/combos`, `/combos/{id}`, `/combos/{id}/components`, `/orders/{id}/combo-lines` (CAT-10 — the last one decomposes a combo into ordinary, correctly-VAT-rated order lines via `AddLine`), `/menu/items/{id}/scheduled-price` (CAT-16 — a pending future price change, resolved lazily by `MenuItem.EffectivePrice` rather than a background job), `/tax-rules`, `/tax-rules/resolve` (CAT-07/08 — effective-dated VAT rate by alcohol band × channel × region, create/list/resolve, not yet wired into `AddLine`), `/client-requirements` (API-07), `/health` (liveness), `/health/ready` (PostgreSQL, OPS-09). Serilog, ProblemDetails, API versioning, idempotency, `X-Brasa-Client` parsing (API-06), RFC 8594 `Deprecation`/`Sunset` headers (API-08, a no-op today), rate limiting per `(tenant, client id)` on `/api/**` (API-12, `429` with `Retry-After`), Brotli/gzip response compression incl. error bodies (API-11), CORS for web clients (`Cors:AllowedOrigins`, `ETag`/`X-Next-Cursor` exposed for browser reads) |
| EF Core + PostgreSQL + RLS | ✅ | **Verified live**, not just asserted: `brasa_app` (unprivileged runtime role) sees zero rows with no tenant set or the wrong tenant set, and cannot run DDL. Re-verified against the new `floor` schema too. See [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| `Modules.Identity` | 🚧 | IDN-01 — `Organization`/`Site`/`Terminal` registry (create + list, no update/delete yet), `Site.Region` a real `PortugueseRegion` from day one. Auth, staff PINs, terminal pairing (the rest of this epic) still I3 |
| `Modules.Catalog` | ✅ | `MenuCategory`, `MenuItem` (incl. optional `Description` and declared `Allergens` — CAT-02), seeded demo menu spanning both VAT bands, soft delete (CAT-18), modifier groups (CAT-03/04), `PriceList`/`PriceListEntry` (CAT-05 — per-site price overrides, create/read/add-entry only, not yet resolved through `AddLine`), `Combo`/`ComboComponent` (CAT-10 — fixed-price bundles; ringing one up *does* reach `AddLine`, decomposing into ordinary lines at allocated prices), scheduled price changes (CAT-16 — `MenuItem.EffectivePrice`, resolved lazily on every read, no background job; `GetMenuAsync` and `AddLine` both resolve through it), `TaxRule` (CAT-07/08 — effective-dated VAT rate by alcohol band × channel × region, create/list/resolve only, not yet wired into `AddLine`/the fiscal document builder) |
| `Modules.Ordering` | ✅ | `Order` aggregate — open against a real `Table` (`TableId`) or as a takeaway with no table at all (ORD-20, `IsTakeaway`), add line with modifiers (price/VAT/modifier snapshot, ORD-05), per-line kitchen notes (ORD-06), split evenly, by item or by cover (ORD-15/16/17), pre-bill preview (ORD-18/19), transfer to a different table (ORD-12, converts a takeaway to dine-in), move a single line onto a different order (ORD-13) or merge two orders (ORD-14, `OrderStatus.Merged`), close, history/search (ORD-22) |
| `Modules.Floor` | ✅ | `Room`, `Table` — full `Free ⇄ Occupied ⇄ Dirty ⇄ Free` lifecycle plus `BillRequested` (FLR-04, `POST /tables/{id}/request-bill`), and `Release()` — `Occupied`/`BillRequested` straight back to `Free`, skipping `Dirty`, used only for table transfers (ORD-12). `xmin`-based optimistic concurrency on `Table` so two concurrent occupy attempts can't both win. `Table.GroupId` (FLR-05) pushes 2+ free tables into one seating unit via `POST`/`DELETE /table-groups`; `Occupy()` itself refuses a grouped table rather than leaving grouping purely cosmetic. Seeded: 2 rooms, 16 tables |
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
| E2E (Playwright) | ✅ | `src/web/e2e` — 141 tests (incl. `admin-shell.spec.ts`/`admin-language-toggle.spec.ts`/`admin-menu-management.spec.ts`, WEB-09/10, `menu-item-schedule.spec.ts` (CAT-11), `menu-item-couvert.spec.ts` (CAT-12), `identity-organization-site-terminal.spec.ts` (IDN-01), `price-lists.spec.ts` (CAT-05), `combos.spec.ts` (CAT-10), `menu-item-scheduled-price.spec.ts` (CAT-16, `test.slow()` — waits out a real ~2s window to prove a due change actually applies, no clock-injection seam exists yet, QA-04), `table-groups.spec.ts` (FLR-05), `tax-rules.spec.ts` (CAT-07/08), and table-label/takeaway-default English assertions in `language-toggle.spec.ts`), all green — hit the QA-02 table-pool-exhaustion limitation again while iterating on `combos.spec.ts` (several partial runs during development left leftover open orders and dirty tables consuming all 16), recovered with the documented runbook (close leftover orders, clear dirty tables); the same flake resurfaced again during CAT-07/08 verification, unrelated to that task's own tests — a different, pre-existing UI spec each time depending on which table a concurrent worker happened to grab, cleanly passing in isolation both times, recovered the same way. A clean single run doesn't need it across several consecutive full runs under real parallel load (2 workers) — that repetition is what surfaced and then proved the fix for the table-occupy race below (and, later, occasionally exhausted the original 8-table pool under back-to-back full runs — a QA-02 scaling limitation, mitigated by doubling the seeded pool to 16). That same repeated-run discipline is what caught the API-10 JSON-casing regression below before it reached a commit, and shaped the API-09 pagination test itself: a first version asserting exact page sizes flaked under concurrent specs sharing the dev database, fixed by walking the full cursor chain and asserting only what must hold regardless of noise from other tests. UI walking-skeleton through the real table picker (QA-05), the modifier picker (CAT-03/04), the pre-bill preview (ORD-18/19), per-line kitchen notes (ORD-06), table transfer (ORD-12), line transfer (ORD-13, API-level), order merge (ORD-14, API-level), split by item and by cover (ORD-16/17, API-level), takeaway orders (ORD-20), menu item description/allergens (CAT-02), menu bulk CSV import (CAT-17), request-bill floor-plan signal (FLR-04), 86-ing a menu item (CAT-13), menu item repricing incl. the past-order-lines-immune-to-a-reprice invariant (CAT-19), menu category visibility (CAT-01), menu `ETag`/304 caching (API-10, now retry-tolerant of legitimate concurrent catalog mutations from sibling specs), idempotency replay — a retried close never double-issues a fiscal document (QA-11), client version negotiation (API-06/07), order-history cursor pagination (API-09), response compression incl. error bodies (API-11), accessibility scans (QA-14), API-level split-math sweep (QA-03), order history/search (ORD-22), language toggle + cookie persistence (WEB-13). CI job written but **not yet run in CI**. See [../development/e2e-testing.md](../development/e2e-testing.md) |

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

> **Update (void a line, ORD-10):** `POST /orders/{id}/lines/{lineId}/void`
> cancels a line after it's already been rung up — a dish that came out
> wrong, say — with a required `reason`; missing or blank is rejected
> outright. The line is never deleted: `ItemName`/`UnitPrice`/`Quantity`
> stay exactly as they were, so an audit trail of what was ordered and
> then cancelled remains visible, and only `LineTotal` drops to zero
> (discount or not). No manager-authorisation gate exists yet — this
> row's own title names one, but IDN-11 is the real gate, once staff
> accounts and roles exist; ships ahead of that trigger the same way
> ORD-11's discounts did. Reuses `BuildFiscalLines` (ORD-11) almost
> unchanged: a voided line is simply skipped when building the fiscal
> document's lines, rather than rendered as a 100%-discount — it was
> never actually delivered, so it never happened from the invoice's point
> of view, while the pre-bill (built from the order's own lines directly,
> not from `BuildFiscalLines`'s output) still lists it with `isVoided: true`
> for staff. Found a real edge case, not one invented for symmetry: voiding
> every line on an order still satisfies `Order.Close()`'s own "at least
> one line" guard (it counts lines, not non-voided ones), but the close
> then correctly fails anyway — `BuildFiscalLines` omits every voided line,
> so the fiscal provider receives zero lines and its own pre-existing
> `fiscal.no_lines` guard rejects it, and because `CloseOrderAsync` never
> persists `Close()`'s transition until the fiscal document actually
> issues, the order is left genuinely `Open`, not silently
> closed-with-nothing-issued. (An earlier draft of this behaviour's own
> doc comment guessed wrong — assumed a zero-value document would be
> issued — before the E2E suite caught the actual `fiscal.no_lines`
> outcome; fixed to describe what happens, not what seemed plausible.)
> **Verified live**: void zeroes the line and drops the order total by
> exactly that amount while leaving the line's own snapshot data
> untouched; a missing/blank reason, an unknown line, a double-void and a
> void-on-a-closed-order are all rejected with the right code; the
> pre-bill/fiscal-document reconciliation invariant holds with a voided
> line in the mix; and closing a fully-voided order 400s with
> `fiscal.no_lines` while leaving the order genuinely still open.

> **Update (`SplitByItem` now discount/void-aware, ORD-16):** the gap
> both discounts (ORD-11) and voiding (ORD-10) explicitly documented and
> left open — `POST /orders/{id}/split/by-item` computed portions from a
> line's raw unit price, not `OrderLine.LineTotal`, so a line-level
> discount or a voided line wasn't reflected in a by-item split preview
> — is now fixed rather than re-documented a third time. Each line's own
> `LineTotal` (already net of any line discount, already zero if voided)
> is split across its quantity via `Money.Allocate` before being handed
> to whichever groups claim those units, so a discounted or voided line
> now splits correctly no matter how its quantity is divided between
> guests. `Order.SplitByItem`'s return shape changed to carry one portion
> per line allocation (grouped like the request) rather than only group
> totals, which incidentally removed a pre-existing duplication:
> `OrderEndpoints`'s per-line breakdown and per-group total used to be
> two independently-computed formulas that had to be kept in sync by
> hand; now the group total is just the sum of the same per-line numbers
> shown in the response, so they can't disagree. Deliberately still open:
> an *order-level* discount isn't prorated into a by-item split, since
> that needs a real product answer (how do you fairly divide "10% off
> the table" between guests who ordered different things?), not a
> guessed-at one. **Verified live**: a 50%-discounted line split across
> two groups gives each its correctly-discounted half; a voided line
> allocated to a group contributes exactly zero; each group's stated
> total still equals the sum of its own line portions and both groups
> still sum to the order's own total; and the existing undiscounted/
> non-voided case (`split-by-item.spec.ts`'s original test) is unchanged.

> **Update (menu item course, CAT-14):** `PUT /menu/items/{id}/course` sets
> or clears which point in the meal a menu item is served at (`Starter`/
> `Main`/`Dessert`/`Drink`). Deliberately independent of `MenuCategory`: a
> category is how the menu is organised for *browsing* (a restaurant might
> group by ingredient or style), a course is *when* it's fired to the
> kitchen — the same dish's category doesn't tell you that. Null means not
> yet assigned, a data-entry gap rather than a claim the item has no
> course, the same convention `Allergens`' empty list already uses. No
> admin/`pos` UI reads it yet, and course *firing* (ORD-07, the actual
> kitchen-sequencing feature) isn't built either — this ships the tag
> ahead of both consumers, the same shape as CAT-02's allergen set before
> any filtering UI existed. Finding this while adding a fresh migration
> also surfaced a real gap in `TenantIsolationIntegrationTests`: it built
> `CatalogDbContext` without the `MigrationsHistoryTable` override
> `Program.cs` and the design-time factory both apply, so its model
> silently disagreed with every committed migration snapshot — invisible
> until EF Core 8's pending-model-changes check actually had a new
> migration to validate against. Fixed by matching the test's setup to
> production, not by suppressing the check. **Verified live**: set,
> persists across a fresh `GET`, clears; an unrecognised course name and an
> unknown item both rejected (`catalog.invalid_course`/`catalog.item_not_found`).

> **Update (menu item kitchen station, CAT-15):** `PUT /menu/items/{id}/station`
> sets or clears which kitchen station prepares an item (`Grill`/`Bar`/
> `ColdKitchen`/`Fryer`/`Pastry`) — independent of both `MenuCategory` and
> `Course` (CAT-14): a starter and a main can both come off the grill.
> Same greenfield, ships-ahead-of-its-consumer shape as CAT-14 — station
> *routing* (KIT-06) needs printers and a KDS that don't exist yet.
> Building it exposed that CAT-14's `TenantIsolationIntegrationTests` fix
> (matching `MigrationsHistoryTable`) was necessary but not sufficient:
> adding this migration hit the identical `PendingModelChangesWarning`
> again, even though the design-time `dotnet ef migrations
> has-pending-model-changes` still reported clean and a source diff showed
> nothing left to fix by hand. Root-caused properly this time by having the
> test call `CatalogDbContextFactory` directly — the same design-time
> factory `dotnet ef` itself uses — instead of a hand-rolled
> `DbContextOptionsBuilder` that had to be kept in sync with it by eye.
> That removes the category of bug entirely: there's no second
> configuration left to drift, whatever the exact EF/Npgsql trigger was.
> **Verified live**: set, persists across a fresh `GET`, clears; an
> unrecognised station name and an unknown item both rejected
> (`catalog.invalid_station`/`catalog.item_not_found`).

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

> **Update (server error messages localized by code, closes ADR 0011's
> known gap):** `pos`'s "Server-sent error text" gap named above — the
> API's `ProblemDetails.title` is always English, a developer-facing
> string, so Portuguese-speaking staff saw raw English error text
> regardless of the language toggle — is now closed. `describeError()`
> (`App.tsx`) looks up `error.code.<code>` in `resources/{pt,en}.ts`
> first (nested to match the code's own dots, e.g.
> `error.code.floor.table_not_dirty`, using `i18n.exists()`/`i18n.t()`),
> falling back to the raw server message with the code shown alongside
> only when no entry exists yet. Scoped deliberately to the ~20 codes
> `pos`'s own API calls (`src/api/client.ts`) can actually trigger, not
> the full registry — `admin`'s equivalent dictionary doesn't exist yet.
> An untranslated code is never silently hidden, just shown in English
> with its code visible until someone adds it. **Verified live**:
> `error-localization.spec.ts` triggers a genuine 409
> (`catalog.item_unavailable`, 86ing an item after the menu already
> rendered — a realistic multi-terminal race) and asserts the exact
> rendered banner text in both Portuguese and English.

> **Update (edit an order line's quantity, ORD-03):** the other half of
> ORD-06's own long-standing caveat — "editing a line itself isn't built
> yet" — is now closed for quantity, the one edit that actually comes up
> in service (a guest asks for one more, or the waiter over-rang before
> anything reached the kitchen). `PUT /orders/{id}/lines/{lineId}/quantity`
> sets a new `Quantity`; `LineTotal` and any discount recompute for free
> since both were already derived from `Quantity` rather than stored
> separately — a percentage discount scales with the new gross, and a
> fixed discount stays fixed but re-clamps via `DiscountAmount`'s existing
> clamp if it would now exceed a smaller line. Deliberately not how a
> wrong or unwanted line is undone, though: that stays `VoidLine` (ORD-10),
> which requires a reason and freezes the line's own `Quantity` for audit —
> `SetLineQuantity` rejects a voided line outright (`order.line_voided`)
> rather than letting an edit quietly reopen what void just closed. This
> is also why ORD-03's own "remove" half stays deliberately unbuilt as a
> separate endpoint: void already is the sanctioned way to take a line off
> the bill, with the audit trail a bare delete would lose. `pos` gets a
> +/− stepper per line (disabled at 1 — dropping to zero is what void is
> for), reusing the same inline-edit real estate `OrderLineNotes` already
> established. **Verified live**: increasing/decreasing recomputes the
> line and order totals to the cent; a percentage discount scales and a
> fixed one re-clamps correctly across a quantity change; a quantity below
> 1, an unknown line, a voided line and a closed order are all rejected
> with the right code; and the stepper in a real browser increases/
> decreases the line and disables "−" at 1.

> **Update (channel pricing — dine-in/takeaway, CAT-06):** `MenuItem`
> gets an optional `TakeawayPrice` (`MapOptionalMoney`, a new sibling to
> `MapMoney` for a money value that's genuinely absent, not zero) — null
> means "same as dine-in," not "free," the same convention as `Course`/
> `Station`. `PUT /menu/items/{id}/takeaway-price` sets or clears it;
> `AddLineAsync` picks `TakeawayPrice ?? Price` when `Order.IsTakeaway`,
> `Price` otherwise, so a line already rung up is unaffected by a later
> change either way — the same snapshot guarantee `Reprice` already gives
> dine-in orders. Deliberately doesn't touch VAT: this row's own title
> names "channel pricing," not "channel tax," and per-channel VAT
> resolution is `TaxRule` (CAT-07/08), a separate, not-yet-built concern
> — `VatRateFraction` is copied onto the line unchanged regardless of
> which price won. Delivery, the third channel this row names, isn't
> built either: there is no delivery order path anywhere in this
> codebase yet, so there is nothing for a delivery price to attach to —
> an honest gap, not an oversight, the same shape as CAT-17's "CSV only"
> caveat for Excel. Shipped with real UI on both sides for once, not
> API-only: `pos`'s menu button shows whichever price the order actually
> being rung up would charge (dine-in vs. takeaway), and `admin` gets an
> inline add/edit/clear editor next to the existing dine-in price field.
> **Verified live**: setting/clearing round-trips through `GET /menu`; a
> takeaway order rings up the takeaway price while a dine-in order for
> the exact same item still charges the dine-in one; negative price and
> unknown item both rejected with the right code; the `pos` menu button
> shows "5,00 €" on a dine-in order and "4,00 €" on a takeaway order for
> the same item; and the `admin` editor's add/edit/clear round-trips to
> the real API, not just local state.

> **Update (floor-plan table CRUD, first slice of FLR-03):** `Table`'s own
> remarks have said since I1 that position/shape exist "so a future
> drag-and-drop editor (FLR-03) has somewhere to persist to without a
> schema change" — this is that persistence layer, not yet the canvas.
> `POST /rooms/{roomId}/tables`, `PUT /tables/{id}` (`Table.Update` —
> label/seats/shape/position, independent of `TableState`) and
> `DELETE /tables/{id}` (`Table.EnsureCanDelete` — requires `Free`, hard
> delete rather than soft: a closed order's `TableLabel` is already
> snapshotted at open time, so nothing needs to re-resolve a table row
> after the fact the way a receipt re-derives a menu item's name would).
> `admin`'s "Plano de sala" nav entry is live for the first time — plain
> add/edit/delete forms per room, numeric X/Y inputs instead of a drag
> canvas, the identical "mechanism first, visual affordance later" call
> WEB-10 already made for the menu editor. Room creation stays a
> deliberate, separate gap: tables can only be added to a room that
> already exists (seeded, FLR-01). Caught and fixed two existing E2E
> assertions that had gone stale the moment this shipped:
> `admin-shell.spec.ts` and `admin-language-toggle.spec.ts` both still
> asserted "Plano de sala" showed the "Brevemente"/"Coming soon"
> placeholder — updated to assert the opposite now that it's genuinely
> live. **Verified live**: create/edit/delete round-trip through
> `GET /floor`; an empty label, zero seats, an unrecognised shape and an
> unknown room are all rejected on create; an unknown table 404s on
> edit/delete; deleting an occupied table 409s (`floor.table_not_free`);
> and the `admin` UI round trip (add → edit → delete) works in a real
> browser end to end.

> **Update (room CRUD, FLR-03's room-CRUD follow-up):** the "Room creation
> is a deliberate, separate gap" caveat named directly above is closed.
> `POST /rooms`, `PUT /rooms/{id}` (`Room.Update` — name/display order,
> independent of the room's tables) and `DELETE /rooms/{id}` (guarded to
> zero tables, checked at the API layer since `Room` has no navigation to
> `Table` — the same sibling-entity shape as every other Floor pair, so
> there's nothing to check from the domain side). `admin`'s "Plano de
> sala" gets an "Add room" form and, per room, an inline rename and a
> delete button disabled (with an explanatory `title`) unless the room
> has no tables left. **Verified live**: create/rename/delete round-trip
> through `GET /floor`; an empty name is rejected on both create and
> rename; an unknown room 404s on update/delete; deleting a room that
> still has tables 409s (`floor.room_not_empty`); and the `admin` UI
> round trip (add → rename → delete) works in a real browser end to end.

> **Update (client-side error tracking, OPS-14):** neither `pos` nor
> `admin` had a React error boundary or any global error handler before
> this — a render-phase exception anywhere in the tree took the whole
> screen to blank white with zero record of what happened, on a POS
> terminal mid-service. `@sentry/react`'s `Sentry.init()` now runs
> unconditionally at startup in both apps, `dsn` read from
> `VITE_SENTRY_DSN` — empty in every committed `.env.example`, the same
> "no real collector/project exists yet" shape OPS-08's OpenTelemetry
> wiring already established — so it never actually sends anything
> anywhere yet, but automatic `window.onerror`/`unhandledrejection`
> capture and a `Sentry.ErrorBoundary` wrapping `<App />` both still work
> locally. The boundary's fallback (`ErrorFallback`, translated) shows a
> plain "something broke, reload" screen instead of a blank one. Proven
> against a genuine thrown error, not just "the boundary component
> exists in the tree": a `DevCrashTrigger` component throws for real when
> the URL carries `?__crashTest=1`, but only inside
> `if (import.meta.env.DEV && ...)` — a literal `false` in a production
> build, so the whole branch is dead-code-eliminated; confirmed by
> grepping both apps' built `dist/assets/*.js` for the trigger's own
> error string and finding nothing. **Verified live**:
> `window.__errorReportingInitialized` (set by this app's own code after
> `Sentry.init()` returns, not a Sentry-internal global relied on as a
> proxy) confirms initialisation completes without throwing on a normal
> page load in both apps; navigating to `?__crashTest=1` in a real
> browser shows the translated fallback screen (not the real app's
> chrome) in both `pos` and `admin`; and that fallback screen passes the
> same WCAG A/AA axe scan (QA-14) as every other screen.

> **Update (database backup + restore drill, OPS-12):** three PowerShell
> scripts (`infra/scripts/`) — `backup-database.ps1` (`pg_dump`, custom
> format), `restore-database.ps1` (`pg_restore` into a target database,
> defaulting to a scratch `_restore_drill`-suffixed name rather than the
> real one, so overwriting live data takes an explicit, deliberate
> override rather than being the default), and `restore-drill.ps1`, which
> chains both together and then does the part that actually makes this a
> *drill* rather than just a backup: enumerates every table across every
> schema and compares row counts, source vs. restored, reporting PASS/FAIL
> per table before cleaning up. Neither script ever pipes the dump through
> a PowerShell text redirect — `pg_dump`'s custom format is binary, and
> Windows PowerShell 5.1's `>`/`Out-File` default to UTF-16LE or
> UTF-8-with-BOM for anything treated as text, which would silently
> corrupt it; both scripts round-trip the file through the container's own
> filesystem via `docker cp` instead, which copies bytes exactly. Found
> this the hard way in the same session, for a different reason: the
> scripts' own doc comments originally used a typographic em dash, and
> Windows PowerShell 5.1 reads a `.ps1` file with no BOM using the system
> codepage, not UTF-8 — the em dash's UTF-8 bytes decoded into three wrong
> characters, one of which broke the string it was inside, and the rest of
> the file printed as literal text instead of running. Fixed by treating
> "no non-ASCII characters in a `.ps1` file" as a hard rule, not a style
> preference, and writing that down in `backup-and-restore.md` so it isn't
> rediscovered later. "Automated" (the scheduling half of this task's own
> title) stays open: there is nothing to schedule it *in* yet — no
> production deployment (OPS-11) and no job runner (OPS-10), so this runs
> on demand today, the same shape as OPS-08/OPS-14 above. **Verified
> live**, not just read for correctness: a real drill against the dev
> database (`brasa-postgres`) passed cleanly — 12 tables across 3 schemas,
> every row count matched (`ordering.orders`: 3508/3508,
> `ordering.order_lines`: 4497/4497, `catalog.menu_items`: 505/505, and so
> on). Both failure paths were deliberately exercised too, not assumed
> correct from the code: two scratch databases seeded with a 3-vs-2 row
> mismatch and a table present in only one were each correctly flagged
> (`FAIL`, `MISSING`) using the same comparison the drill itself runs,
> before this page was written.

> **Update (shared `web/ui` component library, WEB-02):** `pos` and `admin`
> had grown three genuinely identical files each — `formatMoney` (fiscal
> money formatting, always `pt-PT` regardless of the UI language toggle,
> ADR 0011), the `brasa.lang` cookie store, and the `LanguageToggle`
> component itself. `src/web/ui` now holds one copy of each, consumed by
> both apps via a Vite `resolve.alias` (`@brasa/ui` → `../ui/src`) plus a
> matching TypeScript `paths` entry, so the shared files are transformed as
> plain project source — never routed through `node_modules` resolution,
> which would hand Vite's dependency pre-bundler raw, untranspiled TSX and
> break in exactly the way an installed package never would. That alias
> only resolves the shared package's *own* files, though — it does nothing
> for that package's *own* bare imports (`react-i18next`), which still need
> a real `node_modules` reachable by walking up from `ui/src/`. Fixed by
> adding an npm workspace root (`src/web/package.json`, listing `pos`,
> `admin`, `e2e`, `ui`) so `react`/`react-i18next` hoist to a shared
> ancestor `node_modules` — confirmed by `ui`'s own bare imports resolving
> only once that hoist ran, not before. Deliberately narrow scope:
> `errorReporting.ts`/`i18n.ts` themselves stay per-app — Sentry
> initialisation and each app's translation resources differ enough that
> sharing them would trade real duplication for a worse abstraction, not a
> better one. **Verified live**: both apps' `tsc -b`, `vite build` (Rollup,
> not just the dev server) and `oxlint` pass clean; full backend suite (64
> tests) and the full E2E suite (109 tests, 108 passing — the one
> `merge-orders.spec.ts` failure reproduced as a pre-existing table-pool
> contention flake under parallel load, confirmed by re-running it alone,
> where it passed).

> **Update (*prato do dia* daily specials with schedules, CAT-11):**
> `MenuItem.Schedule` is a recurring day/time window — `MenuItemSchedule`
> pairs a `[Flags] ScheduleDays` mask with start/end `TimeOnly` (start
> inclusive, end exclusive; no overnight wraparound, a deliberate v1
> simplification its own doc comment names), set or cleared via
> `PUT /menu/items/{id}/schedule`, all-or-nothing — days, start and end
> together, or all three cleared, never a partial update. Stored as three
> nullable columns via an EF Core complex property (`ComplexProperty`,
> the same shape `Money`/`TakeawayPrice` already use), with `Days` mapped
> through the same well-proven `HasConversion<string>()` a `[Flags]` enum
> already gets everywhere else in this codebase, rather than nesting a
> list conversion inside the complex type — untested territory here, and
> unnecessary when a bitmask says the same thing in one column.
> `GET /menu` (guest/pos-facing) now filters a scheduled item out
> entirely outside its own window; `GET /menu/all` (admin) never filters
> on it, the same visible-vs-management split CAT-01/13 already
> established — an item can be scheduled out and still edited. No
> per-tenant region or site record exists yet to pick a timezone from
> (that's IDN-01/CAT-05 territory), so "now" is resolved via mainland
> `Europe/Lisbon` time through the existing `PortugueseTimeZone` helper —
> an honest gap, written down rather than silently assumed, and the same
> default the rest of the app already uses absent a real site. `admin`
> gets a genuinely new editor (no course/station precedent to follow —
> neither has admin UI yet either): seven day checkboxes plus native
> `<input type="time">` start/end fields, whose `"HH:mm"` value maps
> directly onto the wire format with no conversion. `pos` needed zero
> changes — an out-of-window item is simply absent from `GET /menu`'s
> response, so the existing menu grid already does the right thing.
> **Verified live**: `menu-item-schedule.spec.ts`, self-scheduling —
> "today" and "not today" are computed from the real `Europe/Lisbon`
> date via `Intl.DateTimeFormat`, not hardcoded, so the test never goes
> stale. A window covering today (00:00–23:59) keeps the item on
> `GET /menu`; a window excluding today removes it from `GET /menu` but
> leaves it, schedule and all, on `GET /menu/all`; clearing restores it
> unconditionally. An unrecognised day name, an unparsable time, a
> backwards window (`startTime >= endTime`), a partial update (some but
> not all of days/start/end) and an unknown item are all rejected with
> their own error codes (`catalog.invalid_day_of_week`,
> `catalog.invalid_time`, `catalog.invalid_schedule`,
> `catalog.incomplete_schedule`, `catalog.item_not_found`) — caught by
> `ErrorCodeRegistryTests` (API-04) before the docs were even written,
> exactly the mechanical enforcement it exists for.

> **Update (*couvert* handling, CAT-12):** `MenuItem.IsCouvert` is a plain
> tag via `PUT /menu/items/{id}/couvert` — deliberately not a filter like
> CAT-11's schedule: marking an item couvert never removes it from
> `GET /menu`, and `AddLine` needs no changes at all, since adding a
> couvert item is the same call as any other line. That matters because
> "charged only when consumed" — this row's own title — was already true
> of every menu item before this: nothing is ever added to an order
> except by an explicit `AddLine` call, so there was never a real
> correctness gap here, only a workflow one. What this closes is that
> workflow gap: `pos` gets a dedicated `CouvertBar`, a one-tap strip
> above the regular menu grid that rings a couvert item up at the
> order's own `coverCount` instead of the usual quantity of 1 — offering
> couvert to a table of four is one tap, not four. Hidden entirely for a
> takeaway order (`!order.isTakeaway`), which has no cover count to ring
> up against; the item stays orderable the normal way there too, just
> without the shortcut. `admin` gets a mark/unmark toggle next to the
> existing availability one, with its own badge. **Verified live**:
> `menu-item-couvert.spec.ts` — the flag sets, persists and clears
> without the item ever disappearing from `GET /menu` (confirmed by
> refetching after each change, not just trusting the mutation
> response); a real browser session opens a 3-cover table, taps the
> couvert bar once, and confirms the resulting line reads `3×`, not
> `1×`; a takeaway order shows no couvert bar at all, and the same item
> is still reachable and orderable from the ordinary menu grid; the
> admin toggle round-trips against a live `GET /menu` refetch; an
> unknown item still 404s (`catalog.item_not_found`, already a
> registered code — this task added no new ones).

> **Update (Organization / Site / Terminal hierarchy, IDN-01):**
> `Brasa.Modules.Identity` — previously an empty stub, zero logic, since
> the solution's first commit — now owns a real `identity` schema:
> `Organization` (the restaurant business, tenant-scoped — deliberately
> not assumed 1:1 with `TenantId`, since a single signed-up customer
> running more than one separate brand isn't ruled out, just not
> exercised by anything yet), `Site` (a physical location belonging to
> an organization, carrying a real `PortugueseRegion` — Continental,
> Madeira or Açores — from day one rather than a placeholder), and
> `Terminal` (a bare registry row at a site — pairing, IDN-07, and
> everything that makes a terminal an authenticated actor is separate,
> not-yet-built work). A deliberately narrow slice: create and list only
> (`POST`/`GET /organizations`, `POST`/`GET /organizations/{id}/sites`,
> `POST`/`GET /sites/{id}/terminals`), no update or delete, no auth or
> pairing. This exists to give `Site` a stable, referenceable id — the
> two near-term consumers this unblocks, CAT-05 (price lists per site)
> and FLR-06 (waiter section assignment), are both still unbuilt
> themselves. Followed the Floor module's own `Room`→`Table` shape as a
> direct template (structurally the same "parent has many children,
> both tenant-owned, both plain `Entity` subclasses" pattern), which is
> also where the one easy-to-forget step surfaced again: `dotnet ef
> migrations add` never emits the `RowLevelSecurity.EnableFor` calls a
> new schema needs — those are hand-added to the generated migration
> file every time, in the same commit as the table that needs them, or
> the table's RLS policy silently doesn't exist (exactly the ADR 0010
> failure mode, for a new schema instead of a role misconfiguration).
> `DevIdentitySeeder` seeds one full chain ("Brasa Demo, Lda" →
> "Restaurante Central" → "Caixa 1") on startup, the same role
> `DevFloorSeeder` plays for the floor plan. **Verified live**:
> `identity-organization-site-terminal.spec.ts` — create and list at
> all three levels against the real running API and real Postgres
> (confirming the RLS grant/policy pair actually lets the unprivileged
> `brasa_app` role insert and read, not just that the migration ran);
> the region round-trips exactly; every validation and not-found path
> (`identity.invalid_organization_name`/`invalid_site_name`/
> `invalid_region`/`invalid_terminal_label`/`organization_not_found`/
> `site_not_found`) rejected correctly; and the seeded demo chain
> resolves end to end, organization → site → terminal.

> **Update (price lists per site, CAT-05):** unblocked by IDN-01's `Site`
> the same session — `PriceList` (`SiteId`, `Name`) owns `PriceListEntry`
> rows (`MenuItemId`, `Price`), mirroring the exact ownership shape
> `MenuItem`/`ModifierGroup` already use (a parent aggregate with an
> `AddX` method that both creates the child and enforces the parent's own
> invariants — here, one price per item per list, backed by both a
> domain-level duplicate check and a DB unique index on
> `(PriceListId, MenuItemId)`, the same "guard plus a backstop" shape
> `Table`'s `xmin` token uses for a different race). `SiteId` and
> `MenuItemId` are both plain opaque references, never a live join —
> `Order.TableId`/`OrderLine.MenuItemId`'s own pattern, applied once
> across modules (Catalog → Identity) and once within one (Catalog →
> Catalog). `POST /price-lists` and `GET /sites/{id}/price-lists`
> compose `CatalogDbContext` and `IdentityDbContext` in the same
> handler to confirm a site is real — sanctioned at the API layer, never
> inside either module, per module-boundaries.md rule 5, the same
> composition `OrderEndpoints` already does across four modules.
> `GET /price-lists/{id}/effective-price/{menuItemId}` is the actual
> resolution logic, not just storage: the list's own override when one
> exists, otherwise the item's ordinary price, with a flag saying which
> one the caller got. Deliberately narrow, the same shape IDN-01 itself
> shipped: create, read and add-entry only, no rename/delete/remove-entry;
> and nothing in `AddLine` or either web client resolves an effective
> price through this yet, because neither `pos` nor `admin` has any
> site-selection concept at all today — wiring this into real ordering
> would have no way to know which site an order belongs to. This ships
> the pricing model itself, verified at the API level, the same
> "mechanism before the trigger" shape CAT-14/15 already established for
> course/station tags. **Verified live**: `price-lists.spec.ts` — a
> freshly created item resolves to its own ordinary price with
> `isOverridden: false` before any list entry exists; adding an entry
> flips that to the list's own price and `isOverridden: true`; the entry
> persists across a refetch of the list itself and appears in the site's
> own list of price lists; a duplicate entry for the same item, a
> negative price, an unknown item and an unknown price list are all
> rejected with their own codes (`catalog.price_list_entry_exists`/
> `catalog.invalid_price`/`catalog.item_not_found`/
> `catalog.price_list_not_found`).

> **Update (combos / *menu do dia*, CAT-10):** a combo is deliberately
> never a new fiscal concept. `Combo` (`Name`, `Price`) owns
> `ComboComponent` rows (`MenuItemId`, always exactly one unit — no guest
> choice among several, no quantity greater than one; both real product
> features, deferred rather than guessed at, the same "narrow slice"
> discipline IDN-01/CAT-05 already established), the same ownership shape
> `MenuItem`/`ModifierGroup` and `PriceList`/`PriceListEntry` already use.
> The genuinely new piece is `POST /orders/{id}/combo-lines`
> (`OrderEndpoints.cs`, composing Catalog and Ordering the same way
> `AddLineAsync` already does for a single item): it resolves the combo's
> components, allocates `Combo.Price` across them via `Money.Allocate`
> weighted by each component's own current standalone price — the exact
> same proration ORD-11 already uses to prorate an order-level discount
> across lines, reused rather than reinvented — then adds each component
> as an ordinary `OrderLine` through the existing `Order.AddLine`, at that
> component's own real VAT rate. Portuguese VAT is charged on the
> underlying goods, not the bundle wrapper, so a combo mixing a 13% main
> and a 23% drink cannot legally collapse into one line at one rate; this
> design means it never has to, and the fiscal correctness comes free
> from machinery already proven — a combo is only ever a priced-lower
> bundle of ordinary lines, verified by the exact same
> `FiscalDocumentLine`/`VatBreakdownDto` code every other order already
> exercises. Nothing in `pos`/`admin` offers a "ring up this combo" UI
> yet — verified at the API level only, the same "mechanism before the
> trigger" shape CAT-14/15 already established. **Verified live**:
> `combos.spec.ts` — a combo priced at €6.00, well below its two
> components' combined standalone price (€4.00 at 13%, €3.00 at 23%),
> allocates by weight (4:3) to €3.43/€2.57 exactly, summing back to
> €6.00 by construction (`Money.Allocate`'s own guarantee, not a
> coincidence); the pre-bill's VAT breakdown shows both 13%/23% bands
> separately and reconciles to the same €6.00; a duplicate component, an
> unknown item/combo/order, an empty combo, an unavailable component, an
> empty name and a negative price are all rejected with their own codes.
> Iterating on this suite hit the QA-02 table-pool-exhaustion limitation
> again — several partial test runs during development each left an
> open order (some with zero lines, which `closeOrder` correctly refuses
> to close — `order.empty`) and a dirty table behind, and by the time all
> 16 were consumed, three unrelated specs failed with "No free table
> available." Not a regression: recovered with the documented runbook
> (close every leftover order, adding a line first if it had none, then
> clear every dirty table), and the full suite passed cleanly immediately
> after. A genuine gap this surfaced in the new spec itself, not the
> product: a rejected `POST /orders/{id}/combo-lines` call correctly
> leaves the order with zero lines, and the test's own cleanup then hit
> the same `order.empty` guard `void-line.spec.ts` already documents —
> fixed the same way, giving the order one real line before closing it.

> **Update (menu versioning, scoped to scheduled price changes, CAT-16):**
> the backlog row's own title, "menu versioning with effective dates,"
> covers two genuinely different features — a historical audit trail of
> what the whole menu looked like on a past date, and a change defined now
> that activates automatically in the future. Order lines already snapshot
> their own price and VAT rate at time of sale, so the per-sale history
> half was already solved before this task; this scopes to the other
> half, scheduled activation, which was a real gap. `MenuItem` gained
> `ScheduledNewPrice`/`ScheduledPriceEffectiveFromUtc` — two flat,
> independently-nullable fields, always written together, not one nested
> `ScheduledPriceChange` value object at the persistence layer: the first
> attempt at a single nested `ComplexProperty` (a `Money` inside a
> complex type inside `MenuItem`) failed at `dotnet ef migrations add`
> time with "no suitable constructor was found" — EF Core cannot
> constructor-bind a complex type nested inside another complex type.
> Flattening to two properties, mapped with the same `MapOptionalMoney`
> helper `TakeawayPrice` already uses successfully, avoided the problem
> entirely; `MenuItem.ScheduledPrice` stays a rich, computed (not
> persisted, `.Ignore()`d) property assembling the two for the domain API.
> Deliberately not driven by a background job: nothing in this codebase
> runs one yet (Hangfire is OPS-10, not built). Instead
> `MenuItem.EffectivePrice(nowUtc)` resolves the pending change lazily on
> every read, the exact "computed, never promoted into storage" shape
> CAT-11's own recurring schedule already proved — a change due five
> minutes ago applies correctly with no manual step and no write-during-
> a-read anti-pattern to reason about. This meant touching every call
> site of `MenuItem.ToDto()` (ten, across `CatalogEndpoints.cs`) to pass
> through the caller's own injected `IClock`, since `MenuItemDto.Price`
> now always means the *effective* price, consistently whether `GET
> /menu`, `GET /menu/all`, or any of the other mutation endpoints
> returns it — never a stale figure in one place and a fresh one in
> another. The same fix reached `AddLineAsync` and `AddComboLineAsync`
> (CAT-10): both now resolve `EffectivePrice` before snapshotting a
> line's `unitPrice` or weighting a combo's proportional allocation,
> so a guest is charged exactly what `GET /menu` just displayed, not a
> stale raw value. Deliberately narrow beyond that: only one pending
> change can exist at a time, and only the dine-in `Price` can be
> scheduled, not `TakeawayPrice` — both named, deferred gaps.
> **Verified live**: `menu-item-scheduled-price.spec.ts` — with no
> clock-injection seam for a live API process (QA-04, still nothing
> needs one), this schedules a change roughly 1.5 seconds out, confirms
> `GET /menu` and the item's own response both still show the original
> price with `scheduledPrice.isActive: false`, waits out a real ~2-second
> window, then confirms both `GET /menu` and a real `AddLine` call
> reflect the new price with `isActive: true` and zero intervening
> action; clearing restores the original price; a partial request, a
> non-future date, an unparsable date, a negative price and an unknown
> item are all rejected with their own codes.

> **Update (floor-plan seating groups, FLR-05):** the backlog row's own
> title, "table merge / split for large parties," reads like it could mean
> a full order-merge — that already exists, separately, as table
> transfer/`ORD-14` merge-orders. This scopes to the other reading: pushing
> 2+ physically *free* tables together into one seating unit for a party
> too large for any single table. `Table` gained `GroupId` — a plain
> `Guid?` with no FK, the same opaque-cross-aggregate-reference convention
> `OrderLine.MenuItemId`/`ComboComponent.MenuItemId` already established —
> set by `POST /table-groups` (2+ distinct, all-`Free` table ids) and
> cleared by `DELETE /table-groups/{groupId}`. Mid-design this could have
> shipped as purely cosmetic — nothing stopping a grouped table from still
> being individually seated, which would have actively misled staff
> reading the floor plan rather than helped them. Revised instead to give
> it real teeth without the much larger change cascading `Occupy`/`Clear`/
> `Release` across a group's siblings would have required (a change
> touching every already-shipped table-state endpoint, deliberately
> deferred as a named gap): `Table.Occupy()` itself now refuses a grouped
> table with a new `floor.table_grouped` 409, checked *after* the existing
> free-state check so the two failure reasons stay distinct. No client UI
> yet — no floor-plan multi-select exists in `admin`/`pos` today — the
> same "ship the mechanism ahead of the trigger" call CAT-05/CAT-10/CAT-16
> each made. **Verified live**: `table-groups.spec.ts` — grouping two free
> tables makes `POST /orders` 409 (`floor.table_grouped`) against either
> one; deleting the group restores ordinary seating on both, confirmed by
> actually opening and closing a real order afterward rather than just
> re-reading `GET /floor`; rejects fewer than 2 tables
> (`floor.table_group_too_small`), an unknown table (`floor.table_not_found`),
> a table that isn't `Free` (`floor.table_not_free`), a table already in
> another group (`floor.table_already_grouped`), and 404s deleting an
> unknown group (`floor.table_group_not_found`).

> **Update (effective-dated tax rules, CAT-07/08):** `VatRate` had named
> itself an explicit I0 placeholder from the start — a fixed fraction on
> `MenuItem`, unconfirmed by an accountant, standing in until a real
> effective-dated model existed. `TaxRule` is that model:
> `(IsAlcoholic, IsTakeaway, Region, Rate, EffectiveFromUtc, EffectiveToUtc)`.
> "Item" in this row's own title ("item × channel × region") is the alcohol
> band, not a per-`MenuItemId` key — Portuguese VAT law taxes categories of
> goods, never one named product, the same reason `MenuItem.IsAlcoholic`
> (CAT-09) exists at all. `POST /tax-rules` creates a new effective-dated
> row; there is deliberately no update or delete — correcting a rate means
> adding a new, later-effective row, never editing one already on file, the
> same "never mutate, only add" instinct fiscal documents themselves
> follow even though a `TaxRule` isn't one. `GET /tax-rules/resolve`
> (CAT-08) is the actual lookup: given an alcohol band, channel, region and
> instant (defaulting to now), `TaxRule.Resolve` picks the rule in force,
> and if two rules' ranges wrongly overlap for the same combination — a
> data-entry mistake — the most recently-*started* one wins rather than
> the lookup silently picking whichever happens to sort first. Deliberately
> **not** wired into `AddLine`, `AddComboLineAsync`, or the fiscal document
> builder — all three still read `MenuItem.VatRate` directly. Rewiring
> every one of those call sites to resolve through a live `TaxRule` lookup
> touches the most fiscal-sensitive code in the entire system, and doing
> that safely deserves its own dedicated, carefully-verified pass rather
> than riding in as a side effect of shipping the data model — the same
> "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already
> established. Dev-seeded: mainland dine-in and takeaway rows for both
> bands (13%/23%, the same two rates `VatRate.IntermediateMainland`/
> `StandardMainland` already named), effective from a fixed 2024-01-01
> anchor rather than "now" so resolving "today" always finds a rule
> regardless of which day the seeder happens to run. Madeira/Azores rows
> are deliberately not seeded — no seeded site claims either region yet,
> and inventing rates nobody asked for would be worse than an honest gap.
> Caught one real bug before it shipped: the first version of the seeder
> appended tax-rule seeding *after* `SaveChangesAsync`, guarded only by its
> own idempotency check — but it sat *after* the existing `if (categories
> already exist) return;` early exit, so on this session's own long-lived
> dev database (categories seeded many tasks ago), the whole method
> returned before ever reaching the new code and `GET /tax-rules` came
> back empty. Fixed by moving the tax-rule seeding above that early
> return, with its own independent idempotency check. **Verified live**:
> `tax-rules.spec.ts` — the seeded mainland rates resolve correctly for
> both bands and both channels; on a fresh region owned only by this test,
> a later rule supersedes an earlier one exactly within its own window,
> and resolving before the earliest rule's start 404s
> (`catalog.tax_rule_not_found`); an unrecognised region
> (`catalog.invalid_tax_rule_region`), an out-of-range percentage
> (`catalog.invalid_vat_rate_percent`), an unparsable date
> (`catalog.invalid_tax_rule_date`) and a backwards effective range
> (`catalog.invalid_tax_rule_effective_range`) are all rejected on create;
> an uncovered combination 404s on resolve too.

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
| Seq (log viewer + traces) | ✅ | `http://localhost:5341` — every log line during a request now carries `TenantId` (OPS-07, `TenantLoggingMiddleware`), verified by reading a real request's events straight from the console/Seq sink. Was actually crash-looping until this session: `datalust/seq:latest` now requires an explicit first-run admin password or `SEQ_FIRSTRUN_NOAUTHENTICATION`, and `infra/docker-compose.yml` set neither — the container reported `Up` for a few seconds after every restart before crashing again, easy to miss without actually querying it. Fixed with the no-auth opt-out (fine here: localhost-only, same trust level as Postgres's own dev credentials); re-verified the container stays up and a real request's log line actually lands in Seq, not just that the process didn't exit. Now also receives real distributed traces and metrics (OPS-08, below) via its native OTLP ingestion |
| OpenTelemetry (OPS-08) | ✅ | ASP.NET Core, outbound `HttpClient` and Npgsql tracing + ASP.NET Core/HTTP/.NET runtime metrics, OTLP-exported to Seq — config-bound (`Otel:OtlpEndpoint`), empty by default (no real collector exists for a production deployment, OPS-11), set only in `appsettings.Development.json`. Npgsql tracing goes through `AddSource("Npgsql")` rather than a dedicated package — Npgsql has emitted spans on an ActivitySource literally named `"Npgsql"` since v7, and subscribing to it by name is what turns emission on, independent of whatever exact extension-method surface the separate `Npgsql.OpenTelemetry` package exposes (never actually needed it). Caught a real, silent bug before it shipped: `AddOtlpExporter`'s `Endpoint` is posted to exactly as configured — no automatic `/v1/traces`/`/v1/metrics` suffix, unlike the *different*, unified `UseOtlpExporter()` helper — so the first attempt 404'd against Seq with **no exception anywhere in the app** (export failures are an OpenTelemetry SDK-internal concern by design). Found by temporarily attaching a raw `System.Diagnostics.ActivityListener` to inspect the exporter's own outbound HTTP spans directly — nothing short of that would have surfaced it. **Verified live**: a real request's `Microsoft.AspNetCore` span and its child `Npgsql` query span both land in Seq with correct `ParentId` parent/child linkage and `service.name=brasa-api`, and a periodic metrics export lands too (confirmed via Seq's own `/api/diagnostics/ingestion`-adjacent event stream, not just "the process didn't throw") |
| CI (GitHub Actions) | ✅ | Build gate, tests, vulnerability scan, docs link check. `e2e` job added this session — written and locally-equivalent to what passed on this machine, but not yet exercised by an actual CI run |
| Client-side error tracking, `pos`/`admin` (OPS-14) | ✅ | `@sentry/react`, same "ship the seam, no real collector yet" shape as OpenTelemetry (OPS-08) above: `Sentry.init()` runs unconditionally at startup with `dsn` read from `VITE_SENTRY_DSN` — empty/unset in every committed `.env.example`, since no real Sentry project exists yet (OPS-11's "no infra credentials" gap) — so it never sends anything anywhere, but automatic `window.onerror`/`unhandledrejection` capture and the render-tree `Sentry.ErrorBoundary` both still work locally. Each app's `main.tsx` wraps `<App />` in the boundary with a translated `ErrorFallback` (a friendly "something broke, reload" screen, not a blank one) — both apps duplicate this the same way they duplicate `LanguageToggle`/`money.ts` today. **Verified live**: `window.__errorReportingInitialized` confirms `Sentry.init()` completes without throwing on a normal load in both apps; a dev-only `DevCrashTrigger` (`?__crashTest=1`, `import.meta.env.DEV`-gated — confirmed absent from both apps' production bundles by grepping the built JS) proves the boundary catches a genuine thrown error and renders the fallback instead of a blank screen, in a real browser, in both `pos` and `admin`; the fallback screen itself passes the same WCAG A/AA axe scan (QA-14) as every other screen |
| Database backup + restore drill (OPS-12) | 🚧 | `infra/scripts/{backup-database,restore-database,restore-drill}.ps1` — `pg_dump`/`pg_restore` via `docker exec`+`docker cp` (never a PowerShell text redirect, which would corrupt the binary dump format), restoring into a scratch database and comparing every table's row count across every schema. "Automated" (the scheduling half) is the open gap — nothing to schedule it in yet, no production deployment (OPS-11) and no job runner (OPS-10). See [backup-and-restore.md](../development/backup-and-restore.md). **Verified live**: a real drill against the dev database passed (12 tables, all row counts matched); both failure paths (a count mismatch, a table missing entirely) were deliberately reproduced against scratch databases and correctly flagged, not just assumed correct from the code |
| Load testing (QA-13) | 🚧 | `src/web/e2e/load/{read-load,write-load,run-all}.mjs` (`npm run load`) — read path via `autocannon`, write path (open→add line→close→clear) via a hand-rolled correlated-flow harness bounded by the seeded 16-table pool. Scoped honestly against what this row's own title can't yet mean: no multi-tenant data exists for "50 sites," and `Reporting` is empty so "reporting queries never touch the transactional path" has nothing to test. See [load-testing.md](../development/load-testing.md). **Verified live**: read path p95 199ms (menu) / 112ms (floor) at 20 concurrent connections; write path zero failures across 2,204 mutating requests at 10 concurrent terminals, every operation p95 ≤132ms. Found a real ~40x latency trap along the way, not an app bug: `Debug`-level Serilog logging (the `appsettings.Development.json` default) inflated `GET /menu` from p50 67ms to p50 2583ms purely from logging I/O under concurrency — confirmed by a fast single sequential `curl` at the same log level, which ruled out the query itself, then by re-measuring at `Information` (production's own default) and seeing the ~40x drop |

## Blockers

| # | Blocker | Impact | Owner |
|---|---|---|---|
| 1 | Portuguese legal entity not yet formed | Cannot submit Modelo 24 to AT. **Start now — it is on the critical path for revenue, not for code** | Founder |
| 2 | VAT rules unconfirmed by an accountant | `TaxRule` design absorbs any answer, but rates must be verified before launch | Founder |
| 3 | `Brasa` trademark and domains not cleared | Check INPI (PT), EUIPO (classes 9/42), and `.pt`/`.com` before spending on branding | Founder |
