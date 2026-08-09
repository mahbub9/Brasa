# Build status

> **Purpose:** a project scaffold makes empty things look finished. This page is
> the honest inventory of **which code exists**. Update it in the same commit
> that changes reality.
>
> For **what to build next and task-level progress**, see
> [backlog.md](backlog.md) — 291 tasks with stable IDs. This page is
> component-level; the backlog is task-level.

**Last updated:** 2026-08-09 · **Roadmap phase:** I0 complete except deployment (OPS-11); I1's opening slice (real rooms and tables, FLR) proven end-to-end

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
| `Brasa.Api` | ✅ | `/api/v1/ping`, `/menu` (+ soft-delete), `/floor`, `/orders` (+`GET` search/history — ORD-22, `/takeaway` — ORD-20, `/lines`, `/lines/{id}/notes` — ORD-06, `/lines/{id}/transfer` — ORD-13, `/merge` — ORD-14, `/split`, `/split/by-item` — ORD-16, `/split/by-cover` — ORD-17, `/pre-bill`, `/transfer` — ORD-12, `/close`), `/tables/{id}/clear`, `/health` (liveness), `/health/ready` (PostgreSQL, OPS-09). Serilog, ProblemDetails, API versioning, idempotency, CORS for web clients (`Cors:AllowedOrigins`) |
| EF Core + PostgreSQL + RLS | ✅ | **Verified live**, not just asserted: `brasa_app` (unprivileged runtime role) sees zero rows with no tenant set or the wrong tenant set, and cannot run DDL. Re-verified against the new `floor` schema too. See [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| `Modules.Identity` | 📁 | I3 (auth) |
| `Modules.Catalog` | ✅ | `MenuCategory`, `MenuItem`, seeded demo menu spanning both VAT bands, soft delete (CAT-18), modifier groups (CAT-03/04) |
| `Modules.Ordering` | ✅ | `Order` aggregate — open against a real `Table` (`TableId`) or as a takeaway with no table at all (ORD-20, `IsTakeaway`), add line with modifiers (price/VAT/modifier snapshot, ORD-05), per-line kitchen notes (ORD-06), split evenly, by item or by cover (ORD-15/16/17), pre-bill preview (ORD-18/19), transfer to a different table (ORD-12, converts a takeaway to dine-in), move a single line onto a different order (ORD-13) or merge two orders (ORD-14, `OrderStatus.Merged`), close, history/search (ORD-22) |
| `Modules.Floor` | ✅ | `Room`, `Table` — full `Free ⇄ Occupied ⇄ Dirty ⇄ Free` lifecycle (`BillRequested` transition exists, unused by any endpoint yet), plus `Release()` — `Occupied`/`BillRequested` straight back to `Free`, skipping `Dirty`, used only for table transfers (ORD-12). `xmin`-based optimistic concurrency on `Table` so two concurrent occupy attempts can't both win. Seeded: 2 rooms, 16 tables |
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
| `pos` | ✅ I0/I1 shell | React 19 + Vite 8 + TS: floor table picker (WEB-05, incl. "Nova venda ao balcão" for a takeaway order with no table — ORD-20) → menu, incl. a modifier picker for items with groups (CAT-03/04) → lines, each with an inline kitchen-note editor (ORD-06) → transfer to a different table (ORD-12) → split preview → pre-bill preview (ORD-18/19, clearly labelled *documento não fiscal*) → close → receipt. pt-PT default / en toggle, cookie-persisted (ADR 0011). No auth, no offline, no Dexie yet — those are I2 (see [roadmap.md](roadmap.md)) |
| `kds` | ⬜ | |
| `admin` | ⬜ | |
| `order` (QR self-ordering) | ⬜ | |

## Tests

| Suite | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | 18 passing, incl. exhaustive allocation check and the error-code registry test (API-04) |
| `Brasa.Fiscal.Portugal.Tests` | ✅ | 13 passing: gross→net VAT derivation (exhaustive per rate), mock provider sequential numbering, mixed-rate reconciliation |
| `Brasa.Api.IntegrationTests` | ✅ | 5 tests: `TenantIsolationReflectionTests` (DAT-11, no DB) + `TenantIsolationIntegrationTests` (QA-09/10) — real disposable PostgreSQL via Testcontainers, zero rows with no/wrong tenant, own rows only with the right one, DDL refused. The automated version of the manual check that first caught [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| E2E (Playwright) | ✅ | `src/web/e2e` — 34 tests, all green across several consecutive full runs under real parallel load (2 workers) — that repetition is what surfaced and then proved the fix for the table-occupy race below (and, later, occasionally exhausted the original 8-table pool under back-to-back full runs — a QA-02 scaling limitation, mitigated by doubling the seeded pool to 16). UI walking-skeleton through the real table picker (QA-05), the modifier picker (CAT-03/04), the pre-bill preview (ORD-18/19), per-line kitchen notes (ORD-06), table transfer (ORD-12), line transfer (ORD-13, API-level), order merge (ORD-14, API-level), split by item and by cover (ORD-16/17, API-level), takeaway orders (ORD-20), accessibility scans (QA-14), API-level split-math sweep (QA-03), order history/search (ORD-22), language toggle + cookie persistence (WEB-13). CI job written but **not yet run in CI**. See [../development/e2e-testing.md](../development/e2e-testing.md) |

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
| 4 | Idempotency keys on all mutations | ⬜ | High — auditing every mutation for double-effect |
| 5 | `X-Brasa-Client` header + `client-requirements` endpoint | ⬜ | Medium — without it the first mobile release can never be safely deprecated |
| 6 | Stable error-code contract | 🚧 | Medium — `Error.Code` exists; the stability guarantee is not yet enforced |
| 7 | Push token registration + `IPushChannel` | ⬜ | Low — a migration and an endpoint |
| 8 | OpenAPI generation + breaking-change CI | ⬜ | Low now, but it is what keeps 1–7 honest |

Planned clients: staff handheld, owner dashboard, customer app, native KDS.
Stack undecided — the API stays client-agnostic so any of React Native, Flutter
or native Kotlin/Swift remains open.

## Infrastructure

| Component | State | Notes |
|---|---|---|
| Docker | ✅ | 29.6.2, Compose v5.3.1 |
| PostgreSQL | ✅ | 18.4 container, ICU locale provider, `pt-PT` |
| Seq (log viewer) | ✅ | `http://localhost:5341` |
| CI (GitHub Actions) | ✅ | Build gate, tests, vulnerability scan, docs link check. `e2e` job added this session — written and locally-equivalent to what passed on this machine, but not yet exercised by an actual CI run |

## Blockers

| # | Blocker | Impact | Owner |
|---|---|---|---|
| 1 | Portuguese legal entity not yet formed | Cannot submit Modelo 24 to AT. **Start now — it is on the critical path for revenue, not for code** | Founder |
| 2 | VAT rules unconfirmed by an accountant | `TaxRule` design absorbs any answer, but rates must be verified before launch | Founder |
| 3 | `Brasa` trademark and domains not cleared | Check INPI (PT), EUIPO (classes 9/42), and `.pt`/`.com` before spending on branding | Founder |
