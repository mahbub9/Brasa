# Backlog

The plan of record. Every feature and task, with a stable ID and a status.

**How this relates to the other product docs**

| Doc | Answers |
|---|---|
| **This page** | What are we building, and how far along is each task? |
| [roadmap.md](roadmap.md) | **When** — which increment each task belongs to, and its demo |
| [status.md](status.md) | Which code actually exists and works today? |
| [../features/](../features/) | How does a built feature behave — offline, on failure? |
| [plan.md](plan.md) | Why is the roadmap shaped this way? (historical record) |

> **Delivery is incremental** — vertical slices, each ending in a runnable demo,
> not layer-by-layer. The epic tables below are grouped by area for reference;
> **[roadmap.md](roadmap.md) is what says what to build next.** Tasks from many
> epics land in the same week.

**Rules**

- IDs are **stable and never reused**. Reference them in commits and issues:
  `feat(identity): terminal pairing (IDN-06)`.
- Update the status in the **same commit** as the work. A backlog that lags is
  worse than none, because it is trusted.
- New work gets the next free ID in its epic. Never renumber.

**Legend** ✅ done · 🚧 in progress · ⬜ todo · 🔒 blocked · ⏭ deferred past MVP

**Last updated:** 2026-08-09

---

## Progress

| Epic | Area | Done | Total | Phase |
|---|---|---:|---:|---|
| **FND** | Foundation & shared kernel | 10 | 12 | I0 |
| **OPS** | Infrastructure, CI, observability | 7 | 16 | I0 → ongoing |
| **DOC** | Documentation system | 9 | 10 | I0 → ongoing |
| **API** | API platform & mobile readiness | 10 | 18 | I0 (rest: I3) |
| **DAT** | Persistence, tenancy, RLS | 10 | 11 | I0 |
| **IDN** | Identity & access | 0 | 16 | I3 |
| **CAT** | Catalog & menu | 8 | 19 | I0 (rest: I1) |
| **FLR** | Floor plan & tables | 3 | 7 | I1 |
| **ORD** | Ordering | 16 | 22 | I0 (rest: I2) |
| **SYN** | Offline sync engine | 0 | 13 | I5 |
| **AGT** | Site Agent | 0 | 15 | I4–I5 |
| **KIT** | Kitchen printing & KDS | 0 | 14 | I4 |
| **FIS** | Fiscal engine | 3 | 24 | I0 (rest: I7) |
| **WEB** | Web clients | 5 | 13 | I0 (rest: I1–I8) |
| **PAY** | Payments & cash sessions | 0 | 14 | I6 |
| **RPT** | Reporting | 0 | 12 | I8 |
| **QR** | QR self-ordering | 0 | 9 | Post-I8 |
| **QA** | Automated testing | 7 | 14 | I0–I1 → ongoing |
| **MOB** | Mobile apps | 0 | 12 | Post-launch |
| **DIF** | Differentiators | 0 | 21 | Post-MVP — see [differentiation.md](differentiation.md) |
| | **Total** | **88** | **292** | |

> Phase labels now follow the increments in [roadmap.md](roadmap.md) (I0…I8),
> not the original Month-based sequencing — see
> [ADR 0009](../architecture/decisions/0009-incremental-delivery.md).
>
> 88 of 292 — I0 (backend, `pos` shell with pt/en i18n, a first Playwright
> harness) is done except deployment, I1's opening slice — real rooms and
> tables (FLR) and menu modifiers (CAT-03/04, which turned out to already
> cover ORD-05 too) — is done and proven against a live API, there is now a
> real automated regression test for tenant isolation (QA-09/10, DAT-11)
> instead of only the manual verification that first caught ADR 0010, an
> accessibility scan (QA-14) that found and fixed 5 real contrast failures
> on its first run, the error-code contract (hard rule 11) is now
> mechanically enforced (API-04) rather than just stated, `/health/ready`
> (OPS-09) actually checks PostgreSQL instead of only proving the process
> itself is alive, the pre-bill a table sees before paying (ORD-18/19)
> is provably a *documento não fiscal* — no document number, ATCUD or QR
> anywhere on the wire, and never issued through `IFiscalProvider` —
> `GET /orders` (ORD-22) gives history/search by status, table and
> opened-date range, a line can now carry a free-text kitchen note
> (ORD-06) added after it's rung up, a party can transfer to a
> different table mid-service (ORD-12) with both the old and new table's
> state committing atomically, a single line can move onto a different
> open order instead (ORD-13), two orders can combine onto one table
> (ORD-14, the secondary ending up `Merged` — never `Closed`, since no
> fiscal document was issued for it), and a bill can be split by item
> instead of evenly (ORD-16, exact per-allocation portions, no `Allocate`
> remainder needed) or by cover (ORD-17, reusing `Money.Allocate`'s own
> weighted overload directly), a counter sale can be rung up with no
> table at all (ORD-20, `Order.IsTakeaway`), and a menu item can now declare
> a description and its allergens (CAT-02, still 🚧 — image upload needs
> file storage infra not built yet), and `GET /menu` now answers a repeat
> pull with a bodyless `304` when the client's `If-None-Match` shows it
> already has the current menu (API-10 — deliberately not extended to
> `GET /floor`, whose state changes too often for caching to pay off), and
> the idempotency guarantee every mutation relies on (hard rule 7) now has
> automated proof rather than just a doc comment: replaying the same
> `Idempotency-Key` 3× never creates a second order or issues a second
> fiscal document on a retried close (QA-11), and the API can now tell
> which client is calling and answer "what version do you need to be"
> (API-06/07, `X-Brasa-Client` parsing + `GET /client-requirements`) —
> ahead of any client that sends the header yet, same as CAT-02/CAT-18
> shipped ahead of their UI, and `GET /orders` — the one collection here
> that's genuinely unbounded over a restaurant's lifetime — now paginates
> via an opaque `X-Next-Cursor` header rather than the flat capped `take`
> it shipped with (API-09, additive: the response body shape didn't change),
> and every response is now Brotli/gzip-compressed, including error bodies
> (API-11), and the API's shape is now reviewable in a diff instead of only
> inspectable by running the app ([docs/openapi/v1.json](../openapi/v1.json),
> API-13 — regenerated by hand for now, CI drift-checking is the separate,
> not-yet-built API-14), and a menu can now be bulk-loaded from a CSV file
> instead of one item at a time (CAT-17, still 🚧 — Excel not built; rows
> import independently, so one bad row is reported, not fatal to the file).
> Staff can now flag a table as having asked for the bill, too — `BillRequested`
> (FLR-04) already had its domain transition, CSS and i18n strings sitting
> unused; only the endpoint connecting a "Pedir conta" button to them was
> missing. Same story for 86-ing (CAT-13): `MarkAvailable`/`MarkUnavailable`
> and the `AddLine` guard that respects them both existed since I0, but
> nothing could ever actually set `IsAvailable` to `false` until now — and
> a third instance of the identical shape, `MenuItem.Reprice` (CAT-19,
> newly minted): the domain method and its negative-price guard existed
> since I0 with no caller either, and past order lines were already
> immune to a future reprice by construction (they snapshot the price at
> add-time), so the only missing piece really was the endpoint — and a
> fourth, one level up: `MenuCategory.IsVisible` had no setter *at all*,
> even though CAT-01's own title names "visibility" as in scope and the
> row was already marked done. Hiding a category now removes it and every
> item under it from the menu in one call, and the OpenAPI document was
> regenerated to catch up with all five of those endpoints, which had each
> skipped the documented hand-regeneration step. I1's back-office shell
> (WEB-09) now exists too — `src/web/admin`, a second web client — with one
> live screen proving it, not just scaffolding: real category/item/room/table
> counts pulled from `GET /menu` and `GET /floor`, while Menu/Floor/Staff are
> labelled "Brevemente" rather than silently absent, since WEB-10/11 (the
> actual editors) aren't built yet
> (details:
> [status.md](status.md#i0-demo-verified-live-not-just-unit-tested)). Every
> epic marked "I0 (rest: …)" is intentionally partial: I0 builds only the
> single vertical slice the walking-skeleton demo needs, not a whole epic.

---

## FND — Foundation & shared kernel

| ID | Task | Status |
|---|---|---|
| FND-01 | .NET 10 solution, 14 projects, modular monolith structure | ✅ |
| FND-02 | Central package management, zero-warning build policy | ✅ |
| FND-03 | `Money` — integer minor units, allocation-based splitting | ✅ |
| FND-04 | `Result` / `Error` — expected failures as values | ✅ |
| FND-05 | `ITenantContext` / `TenantContext` — resolve once per scope | ✅ |
| FND-06 | `IClock`, `PortugueseRegion`, business-day calculation | ✅ |
| FND-07 | `Entity` base — UUIDv7, tenant-owned, auditable, soft-delete | ✅ |
| FND-08 | Integration event and outbox **contracts** | ✅ |
| FND-09 | API host bootstrap — Serilog, ProblemDetails, health | ✅ |
| FND-10 | Site Agent worker host | ✅ |
| FND-11 | In-process integration event **dispatcher** implementation | ⬜ |
| FND-12 | Outbox processor — polling, retry, backoff, poison handling | ⬜ |

## DAT — Persistence, tenancy, RLS

| ID | Task | Status |
|---|---|---|
| DAT-01 | EF Core + Npgsql wiring, connection resilience | ✅ |
| DAT-02 | Schema-per-module conventions | ✅ `catalog` / `ordering` schemas |
| DAT-03 | `Money` value converter / owned type mapping | ✅ `MapMoney` |
| DAT-04 | Global query filter for `ITenantOwned` | ✅ |
| DAT-05 | PostgreSQL RLS policies on every tenant-owned table | ✅ **verified live** — see [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| DAT-06 | Session variable set from `ITenantContext` per request | ✅ `TenantSessionInterceptor` |
| DAT-07 | Privileged role + `ResolveAsSystem()` path for background jobs | ⬜ flag exists; connection path not designed — see [multi-tenancy.md](../architecture/multi-tenancy.md#the-system-context) |
| DAT-08 | Audit interceptor — `CreatedAt/By`, `ModifiedAt/By` | ✅ `TenantAwareDbContext.StampEntities` |
| DAT-09 | `AssignTenant` interceptor on insert | ✅ |
| DAT-10 | Initial migration + migration-on-startup policy | ✅ migrate-on-startup, elevated role only |
| DAT-11 | Reflection test: every entity is `ITenantOwned` or allow-listed | ✅ `TenantIsolationReflectionTests` — one per module's built EF model, no DB connection needed |

## API — API platform & mobile readiness

> Rules: [../architecture/api-contract.md](../architecture/api-contract.md).
> These are the seams that let Android and iOS ship with no backend change.

| ID | Task | Status |
|---|---|---|
| API-01 | `/api/v1` versioning via `Asp.Versioning` | ✅ literal prefix + `ApiVersionSet`, not a templated segment — see commit message for why |
| API-02 | `/api/public/v1` consumer surface, separated from tenant API | ⬜ |
| API-03 | ProblemDetails mapping from `ErrorType` → HTTP status | ✅ |
| API-04 | Stable error-code registry + test that codes never change meaning | ✅ [error-codes.md](../architecture/error-codes.md), enforced by `ErrorCodeRegistryTests` — scans every `Error.*(...)` call site, fails on a removed/renamed code, an undocumented new one, or a code whose `Type` (and therefore HTTP status) changed. Verified it actually catches drift, not just that it compiles |
| API-05 | `Idempotency-Key` middleware + store | ✅ **verified live**: replayed request returns identical order id; DB confirms one row. In-memory store — durable store needed before scaling out |
| API-06 | `X-Brasa-Client` header parsing (id / version / platform) | ✅ `ClientVersionMiddleware` — best-effort: no client sends this header yet, so a missing/malformed value never fails the request, it just skips enrichment. Parsed `ClientInfo` is stashed on `HttpContext.Items` for `GET /client-requirements` (API-07) and pushed into Serilog's `LogContext` (`ClientId`/`ClientVersion`/`ClientPlatform`) so every log line for the request carries it — **verified live** via the console/Seq output |
| API-07 | `GET /client-requirements` — min & recommended version, sunset | ✅ Looks up the calling client's id (from `X-Brasa-Client`, API-06) in a config-bound `ClientRequirements` section — no admin UI to edit this yet, so it's configuration, not a database table. **Verified live**: known client id → `200` with its policy; missing/malformed header → `400 client.header_required`; well-formed header naming an unconfigured client id → `404 client.unknown_client_id` |
| API-08 | RFC 8594 `Deprecation` / `Sunset` response headers | ⬜ |
| API-09 | Cursor pagination helper, applied to every collection | ✅ `CursorPagination` (opaque base64 bookmark token) applied to `GET /orders` (ORD-22) — the only genuinely unbounded collection today; `/menu` and `/floor` are both bounded by the restaurant's own size and don't need it yet. Additive, not a body-shape change: the response is still a bare array exactly as it shipped, an `X-Next-Cursor` response header carries the next page's bookmark (present only when the page came back full). **Verified live**: page 1 returns the header, page 2 fetched with it returns older, non-overlapping rows; a malformed `cursor` 400s (`order.invalid_cursor`) |
| API-10 | `ETag` / `If-None-Match` on config and menu reads | ✅ `GET /menu` only — deliberately not `GET /floor`, whose state changes continuously through service. **Verified live**: 200 with a computed `ETag` on first pull, 304 with no body when it's echoed back as `If-None-Match`. Caught a real bug in review: the helper's own JSON serialization used `System.Text.Json`'s default (PascalCase) instead of ASP.NET Core's configured camelCase, silently breaking the `pos` client — fixed by resolving the app's configured `JsonSerializerOptions` from DI instead of using the type default |
| API-11 | Response compression | ✅ Brotli + gzip, `EnableForHttps = true` — safe here since the API has no cookie-reflected secrets for BREACH to exploit (bearer-token auth, ADR 0008). `application/problem+json` added to the default MIME type list so error responses compress too, not just success bodies. **Verified live**: `br` when offered, falls back to `gzip`, uncompressed when the client sends no `Accept-Encoding`, and confirmed it doesn't interfere with `ETag`'s `304` path (API-10) |
| API-12 | Rate limiting, keyed by client and tenant | ⬜ |
| API-13 | OpenAPI document generation, committed to the repo | ✅ [docs/openapi/v1.json](../openapi/v1.json), generated by `Microsoft.AspNetCore.OpenApi` (already wired for the dev-only Swagger-style UI) and committed so the API's shape is reviewable in a diff. Regenerated by hand for now — CI enforcement that it hasn't drifted is API-14, deliberately not built yet |
| API-14 | CI breaking-change detection against previous OpenAPI | ⬜ |
| API-15 | TypeScript SDK generation into `web/sdk` | ⬜ |
| API-16 | SignalR hub, JSON protocol | ⬜ |
| API-17 | REST equivalent for every realtime message (enforced by test) | ⬜ |
| API-18 | `/.well-known/` app-link documents for iOS and Android | ⬜ |

## IDN — Identity & access

| ID | Task | Status |
|---|---|---|
| IDN-01 | Organization / Site / Terminal hierarchy | ⬜ |
| IDN-02 | User accounts, email verification, password reset | ⬜ |
| IDN-03 | OAuth 2.1 / OIDC authorization-code flow with PKCE | ⬜ |
| IDN-04 | Access token (JWT) issuance and validation | ⬜ |
| IDN-05 | Refresh token — opaque, rotating, device-bound, replay detection | ⬜ |
| IDN-06 | Device registry — register, list, revoke individually | ⬜ |
| IDN-07 | Terminal pairing via short-lived device code | ⬜ |
| IDN-08 | Staff PIN sign-in on a paired terminal | ⬜ |
| IDN-09 | PIN hashing, lockout, and rotation policy | ⬜ |
| IDN-10 | Roles & permissions model | ⬜ |
| IDN-11 | Manager-authorisation flow for privileged actions (voids, discounts) | ⬜ |
| IDN-12 | Consumer identity realm for the public surface | ⬜ |
| IDN-13 | Tenant provisioning / onboarding | ⬜ |
| IDN-14 | Push token registration endpoints | ⬜ |
| IDN-15 | `IPushChannel` abstraction (no provider adapter yet) | ⬜ |
| IDN-16 | Per-tenant, per-platform feature flags | ⬜ |

## CAT — Catalog & menu

| ID | Task | Status |
|---|---|---|
| CAT-01 | Menu categories, ordering, visibility | ✅ `MenuCategory.IsVisible` had no setter at all until now — nothing could ever set it to anything but its default `true`, despite this row's own title naming "visibility" as in scope and being marked done. `PUT /menu/categories/{id}/visibility` (found by the same sweep as FLR-04/CAT-13/CAT-19 — a domain gap one level up, a category rather than an item) closes it: hiding a category removes it *and every item under it* from `GET /menu` in one call. Ships ahead of any UI. **Verified live**: hide → category and its items vanish from the menu; show → both restored; unknown category `404`s (`catalog.category_not_found`) |
| CAT-02 | Menu items — name, description, image, allergens | 🚧 `PUT /menu/items/{id}/details` sets description + declared allergens (14 fixed EU-regulated allergens, Regulation (EU) No 1169/2011 — stable taxonomy, not a Portugal-specific figure needing an accountant's confirmation like `VatRate`); rendered on the `pos` menu screen. Image upload still not built — needs file storage infra |
| CAT-03 | Modifier groups (required / optional, min / max) | ✅ `ModifierGroup` belongs to one `MenuItem` (not yet shared across items — see its doc comment); server enforces min/max on `POST /orders/{id}/lines`, not just the UI |
| CAT-04 | Modifiers with price deltas | ✅ `Modifier.PriceDelta` (can be negative — e.g. "Meia dose"); snapshotted onto `OrderLineModifier` at the time of sale, folded into `LineTotal` and the fiscal document's gross total |
| CAT-05 | Price lists per site | ⬜ |
| CAT-06 | Channel pricing — dine-in / takeaway / delivery | ⬜ |
| CAT-07 | `TaxRule` — item × channel × region, effective-dated | ⬜ `VatRate` ships as the explicit I0 placeholder — see its doc comment |
| CAT-08 | VAT resolution service with date-aware lookup | ⬜ |
| CAT-09 | Alcohol flag driving the 23% band separation | ✅ `MenuItem.IsAlcoholic` |
| CAT-10 | Combos / menus (*menu do dia*) | ⬜ |
| CAT-11 | *Prato do dia* — daily specials with schedules | ⬜ |
| CAT-12 | *Couvert* handling — charged only when consumed | ⬜ |
| CAT-13 | Item availability / 86-ing (out of stock) | ✅ `MarkAvailable`/`MarkUnavailable` existed since I0 and `AddLine` already enforced `IsAvailable`, but no endpoint ever called either — `IsAvailable` could never actually become `false`. `PUT /menu/items/{id}/availability` closes that: ships ahead of any UI that will call it (no admin app, no in-order 86 control), same as CAT-02/CAT-17/CAT-18. **Verified live**: 86'ing an item hides it from `GET /menu` and the previously-dead `catalog.item_unavailable` guard on `AddLine` finally fires for real; un-86'ing restores both; unknown item `404`s |
| CAT-14 | Course assignment per item | ⬜ |
| CAT-15 | Kitchen station routing per item | ⬜ |
| CAT-16 | Menu versioning with effective dates | ⬜ |
| CAT-17 | Bulk import (CSV / Excel) | 🚧 `POST /menu/items/import` — CSV only, Excel not built. Hand-written RFC 4180 parser (`CsvParser`, 8 unit tests — quoting, escaped quotes, embedded newlines, CRLF/LF, blank lines), no new dependency. Rows import independently — an unknown category or an unparsable price is reported per-row (1-indexed against the data rows) rather than failing the whole file. Create-only, not upsert: importing the same file twice creates duplicates. **Verified live**: 2 valid + 2 invalid rows in one file → `created: 2`, two row-level errors with the exact bad value named; empty CSV and a header missing a required column both `400` |
| CAT-18 | Soft delete preserving historical order references | ✅ `MenuItem` only (what `OrderLine.MenuItemId` can reference) — `DELETE /menu/items/{id}`, no admin UI yet. Verified live: deleted item vanishes from `/menu` and can't be re-ordered, but a past order's line keeps its name/price. See `ISoftDeletable` in `docs/architecture/multi-tenancy.md` |
| CAT-19 | Menu item price editing | ✅ `MenuItem.Reprice` existed with its own negative-price guard since I0, with no endpoint calling it — found the same way as CAT-13's availability gap. `PUT /menu/items/{id}/price` closes it; ships ahead of any UI, same as CAT-02/13/17/18. Safe by construction, not convention: `OrderLine.UnitPrice` snapshots at add-time, so repricing never rewrites a past order. **Verified live**: reprice a seeded item, confirm the *already-open* order's existing line total is unchanged while `GET /menu` shows the new price; negative price and unknown item both rejected (`catalog.invalid_price`/`catalog.item_not_found`) |

## FLR — Floor plan & tables

| ID | Task | Status |
|---|---|---|
| FLR-01 | Rooms / areas (indoor, esplanada, bar) | ✅ `Room` — seeded (Salão, Esplanada), no editor UI yet |
| FLR-02 | Tables — number, seats, position, shape | ✅ `Table` — position/shape stored for FLR-03 to use later; `pos` renders a static grid, not the coordinates |
| FLR-03 | Drag-and-drop floor plan editor | ⬜ back-office feature (WEB-10), needs `admin` app |
| FLR-04 | Table states (free, occupied, bill requested, dirty) | ✅ all four wired end-to-end through `pos`, including `BillRequested` now: `POST /tables/{id}/request-bill` + a "Pedir conta" button, distinct from the pre-bill preview (ORD-18/19, "Ver conta") — that stays a read-only `GET`, this is the explicit floor-plan signal for staff. **Verified live** in a real browser: clicking it flags the table `BillRequested` on `GET /floor`; a free table 409s (`floor.table_not_occupied`), an unknown table 404s |
| FLR-05 | Table merge / split for large parties | ⬜ |
| FLR-06 | Section assignment to waiters | ⬜ depends on IDN |
| FLR-07 | Multi-floor support | ⬜ |

## ORD — Ordering

| ID | Task | Status |
|---|---|---|
| ORD-01 | Order aggregate — lifecycle and state machine | ✅ `Open`/`Closed`; richer states (courses, kitchen status) are I2 |
| ORD-02 | Open a table, set cover count | ✅ opens against a real `Table` (FLR), not free text — see `Order.TableId` |
| ORD-03 | Add / remove / edit order lines | 🚧 add only — remove/edit are I2 |
| ORD-04 | Line snapshots — name, price, VAT rate at time of sale | ✅ |
| ORD-05 | Apply modifiers to a line | ✅ shipped alongside CAT-03/04 — `AddLine`'s `selectedModifierIds` resolved and validated at the API layer (`ResolveModifiers`), folded into `OrderLine.ModifiersTotal`/`LineTotal` |
| ORD-06 | Free-text kitchen notes | ✅ `PUT /orders/{id}/lines/{lineId}/notes` — per-line, set after the line is rung up (editing a line itself is still I2/ORD-03); staff/kitchen visibility only, never a Fiscal concern |
| ORD-07 | Courses and course firing | ⬜ |
| ORD-08 | Send to kitchen (partial and full) | ⬜ |
| ORD-09 | Order line status tracking | ⬜ |
| ORD-10 | Void a line, with reason and manager authorisation | ⬜ |
| ORD-11 | Discounts — line, order, percentage and fixed | ⬜ |
| ORD-12 | Transfer table | ✅ `POST /orders/{id}/transfer` — moves an open order to a different `Free` table. Order status checked before either table is touched; the old table's `Release()` and the new table's `Occupy()` then commit atomically together in one `FloorDbContext.SaveChangesAsync` |
| ORD-13 | Transfer individual lines between tables | ✅ `POST /orders/{id}/lines/{lineId}/transfer` — moves one line onto a different open order. Pure Ordering, no Floor involvement (unlike ORD-12). No `pos` UI yet — deliberately: picking *another* currently-open order is a real product-design question, same scoping call already made for ORD-22 |
| ORD-14 | Merge orders | ✅ `POST /orders/{id}/merge` — moves every line from a secondary open order into the primary, marks the secondary `Merged` (new terminal status, distinct from `Closed`: no fiscal document was ever issued for it), frees its table directly via `Release()`. No `pos` UI yet, same scoping call as ORD-13 |
| ORD-15 | Split bill evenly (`Money.Allocate`) | ✅ **verified live**: 22.60 EUR → 7.54/7.53/7.53, sums to the cent |
| ORD-16 | Split bill by item | ✅ `POST /orders/{id}/split/by-item` — a preview, like ORD-15, but needs a structured body (which line/quantity goes to which guest) so it's a `POST`. Every line's quantity must be allocated exactly once across the groups; each portion is an exact multiple of the line's own price, so unlike `SplitEvenly` this never needs `Allocate`'s remainder distribution |
| ORD-17 | Split bill by cover | ✅ `GET /orders/{id}/split/by-cover?covers=2&covers=3` — reuses `Money.Allocate(ReadOnlySpan<int>)` directly, the exact "split unevenly by covers" case that overload's own remarks call out. Weights must sum to the order's `CoverCount` |
| ORD-18 | Pre-bill — *documento não fiscal*, correctly labelled | ✅ `GET /orders/{id}/pre-bill` — reuses `FiscalDocumentLine`'s gross→net/VAT math purely as a calculator, never calls `IFiscalProvider`; `PreBillDto` has no document number/ATCUD/QR field at all, plus a `documentKind` discriminator, so it can't be mistaken for an invoice on the wire |
| ORD-19 | Reprint pre-bill (must match the original exactly) | ✅ pre-bill is never persisted or numbered, so requesting it any number of times against an unchanged order reproduces identical figures — verified live (`pre-bill.spec.ts`), not just by construction |
| ORD-20 | Takeaway and counter-sale flow | ✅ `POST /orders/takeaway` — pure Ordering, no Floor at all. `Order.IsTakeaway` is the real signal (`TableId` stays `Guid.Empty`, never treated as magic elsewhere); transferring a takeaway order onto a real table (ORD-12) converts it to dine-in. `pos` gets a "Nova venda ao balcão" entry point on the table picker |
| ORD-21 | Order ownership + concurrent-terminal conflict protocol | ⬜ |
| ORD-22 | Order history and search | ✅ `GET /orders` — filter by `status`/`tableId`/`openedFrom`/`openedTo`, capped `take` (1–200, default 50). Returns the lighter `OrderSummaryDto`, not full line detail |

## SYN — Offline sync engine

| ID | Task | Status |
|---|---|---|
| SYN-01 | Client outbox schema (IndexedDB / SQLite) | ⬜ |
| SYN-02 | `POST /sync/push` — idempotent mutation batch | ⬜ |
| SYN-03 | `GET /sync/pull` — cursor-based delta | ⬜ |
| SYN-04 | Opaque server-issued cursor (never a timestamp) | ⬜ |
| SYN-05 | Conflict resolution policy per entity type | ⬜ |
| SYN-06 | Client-side queue with retry and backoff | ⬜ |
| SYN-07 | Connectivity detection and mode switching | ⬜ |
| SYN-08 | LAN-first, cloud-fallback endpoint resolution | ⬜ |
| SYN-09 | Sync status UI — pending count, last sync, errors | ⬜ |
| SYN-10 | Initial full-sync / bootstrap for a new terminal | ⬜ |
| SYN-11 | Compaction of superseded local mutations | ⬜ |
| SYN-12 | Clock-skew tolerance | ⬜ |
| SYN-13 | Chaos tests — kill network mid-order, mid-payment, mid-print | ⬜ |

## AGT — Site Agent

| ID | Task | Status |
|---|---|---|
| AGT-01 | SQLite local store + EF Core model sharing | ⬜ |
| AGT-02 | Pairing flow with the cloud | ⬜ |
| AGT-03 | LAN REST API for terminals | ⬜ |
| AGT-04 | LAN SignalR hub | ⬜ |
| AGT-05 | mDNS / discovery so terminals find the agent | ⬜ |
| AGT-06 | Outbox sync to cloud | ⬜ |
| AGT-07 | Config pull from cloud | ⬜ |
| AGT-08 | Fiscal key custody + at-rest protection | ⬜ |
| AGT-09 | Offline document signing | ⬜ |
| AGT-10 | Series counter with crash-safe persistence | ⬜ |
| AGT-11 | Health endpoint and diagnostics | ⬜ |
| AGT-12 | Installer / deployment (Windows Service or container) | ⬜ |
| AGT-13 | Auto-update with version pinning | ⬜ |
| AGT-14 | Agent-down degraded mode (reserve series) | ⬜ |
| AGT-15 | Remote log shipping | ⬜ |

## KIT — Kitchen printing & KDS

| ID | Task | Status |
|---|---|---|
| KIT-01 | ESC/POS command builder | ⬜ |
| KIT-02 | TCP printer transport | ⬜ |
| KIT-03 | USB / serial printer transport | ⬜ |
| KIT-04 | Printer configuration and station mapping | ⬜ |
| KIT-05 | Ticket layout templates | ⬜ |
| KIT-06 | Station routing rules | ⬜ |
| KIT-07 | Print retry, queue, and failure surfacing on the POS | ⬜ |
| KIT-08 | Printer-down fallback rerouting | ⬜ |
| KIT-09 | Cash drawer kick | ⬜ |
| KIT-10 | KDS — station views | ⬜ |
| KIT-11 | KDS — bump and recall | ⬜ |
| KIT-12 | KDS — prep timers and colour-coded ageing | ⬜ |
| KIT-13 | KDS — course firing controls | ⬜ |
| KIT-14 | Verified hardware shortlist + test matrix | ⬜ |

## FIS — Fiscal engine

> ⚠️ Certification-relevant. Read [../fiscal/README.md](../fiscal/README.md)
> before starting any of these.

| ID | Task | Status |
|---|---|---|
| FIS-01 | `IFiscalProvider` abstraction | ✅ |
| FIS-02 | `Fiscal.Mock` deterministic provider | ✅ every value `MOCK-`-prefixed; VAT correctly derived from gross price |
| FIS-03 | Production guard — mock must never load in Production | ✅ enforced at DI registration, not just documented |
| FIS-04 | `FiscalSeries` entity and lifecycle | ⬜ |
| FIS-05 | AT webservice client — series registration | ⬜ |
| FIS-06 | Série validation code storage | ⬜ |
| FIS-07 | ATCUD generation | ⬜ |
| FIS-08 | RSA key loading and signing | ⬜ |
| FIS-09 | Signature chain — hash of previous document in series | ⬜ |
| FIS-10 | Gapless sequential numbering, crash-safe | ⬜ |
| FIS-11 | QR code payload to AT field spec | ⬜ |
| FIS-12 | QR rendering at ≥30×30mm | ⬜ |
| FIS-13 | Document type `FS` — fatura simplificada | ⬜ |
| FIS-14 | Document type `FT` — fatura with NIF | ⬜ |
| FIS-15 | Document type `FR` — fatura-recibo | ⬜ |
| FIS-16 | Document type `NC` — nota de crédito | ⬜ |
| FIS-17 | Non-fiscal document generation | ⬜ |
| FIS-18 | Immutability enforcement — no update path to issued documents | ⬜ |
| FIS-19 | Append-only fiscal audit log | ⬜ |
| FIS-20 | Chain verification job with alerting | ⬜ |
| FIS-21 | SAF-T (PT) XML export | ⬜ |
| FIS-22 | SAF-T XSD validation in CI | ⬜ |
| FIS-23 | Monthly SAF-T submission job (by the 5th) with retry and paging | ⬜ |
| FIS-24 | Golden-file test suite | ⬜ |

## WEB — Web clients

> React + TypeScript, PWA. `pos`, `kds`, `admin`, `order` each get their own
> app; `web/ui` and `web/sdk` are shared across all four. This epic covers the
> client shells themselves — the domain work they call into (menu, orders,
> floor plan) is tracked in its own epic (CAT, ORD, FLR, ...).

| ID | Task | Status |
|---|---|---|
| WEB-01 | `pos` minimal shell — open table, ring up, split preview, close | ✅ I0: one screen, no auth, no offline. See [status.md](status.md#web-clients) |
| WEB-02 | Shared `web/ui` component library | ⬜ |
| WEB-03 | Shared `web/sdk` — OpenAPI-generated typed client | ⬜ replaces `pos`'s hand-written `src/api/` (I0 placeholder) |
| WEB-04 | `pos` — Dexie local store and offline-first data layer | ⬜ I2, depends on SYN |
| WEB-05 | `pos` — floor plan / table selection screen | ✅ `TablePicker` — static grid per room, colour-coded by state, tap Free to open / tap Dirty to clear. Not the drag-and-drop layout (FLR-03) |
| WEB-06 | `pos` — menu browsing with modifiers and courses | 🚧 Modifiers done — `ModifierPicker.tsx` (CAT-03/04), required single-select and optional multi-select groups both proven live in `modifiers.spec.ts`. Courses not built — deliberately deferred with ORD-07/08/09 (kitchen firing), which is what "courses" in a POS menu screen actually means; there is no KDS yet for a fired course to go to |
| WEB-07 | `pos` — staff PIN login screen | ⬜ depends on IDN |
| WEB-08 | `kds` shell — station view, bump, prep timers | ⬜ I4 |
| WEB-09 | `admin` shell — back-office SPA scaffold | ✅ `src/web/admin` — Vite + React + TS, same tooling as `pos`. One live screen ("Visão geral"/"Overview"): real counts from `GET /menu`/`GET /floor`, proving the shell is actually wired to the API rather than a static mock. Menu/Floor/Staff nav entries are labelled "Brevemente"/"Coming soon" (not silently missing) until WEB-10/11 build their editors. Full pt/en i18n toggle (WEB-13's own ADR 0011 pattern, same `brasa.lang` cookie as `pos` so the preference carries across both apps) — genuine English words throughout, not just a token toggle, since not every staff member reading the English UI is a Portuguese speaker. No auth yet (depends on IDN). **Verified live**: `admin-shell.spec.ts`, `admin-language-toggle.spec.ts` |
| WEB-10 | `admin` — menu and floor-plan editors | ⬜ |
| WEB-11 | `admin` — staff, roles and reporting screens | ⬜ |
| WEB-12 | `order` shell — QR self-ordering PWA | ⬜ Post-I8 |
| WEB-13 | i18n — pt default / en toggle, cookie-persisted, mobile storage seam | ✅ i18next, `src/i18n/`. See [ADR 0011](../architecture/decisions/0011-i18n.md). Extended after real-world feedback (Brasa's actual floor/kitchen staff are not all Portuguese speakers): seeded table labels ("Mesa 1") now render as "Table 1" in English via `src/lib/tableLabel.ts`, and a blank takeaway ticket defaults to "Takeaway" instead of leaking the API's own Portuguese default ("Levantamento") — both are generic operational words, not identity-bearing content like a dish name, so they don't fall under the menu-item exception. **Verified live**: `language-toggle.spec.ts` |

## PAY — Payments & cash sessions

| ID | Task | Status |
|---|---|---|
| PAY-01 | Payment / tender model | ⬜ |
| PAY-02 | Cash tender with change calculation | ⬜ |
| PAY-03 | Card tender, manually captured from a standalone TPA | ⬜ |
| PAY-04 | Split tender across multiple methods | ⬜ |
| PAY-05 | Partial payment against an open order | ⬜ |
| PAY-06 | Tips — recording and attribution | ⬜ |
| PAY-07 | Refunds via credit note | ⬜ |
| PAY-08 | Cash session — *abertura de caixa* with float | ⬜ |
| PAY-09 | Cash movements — pay-in, pay-out, with reason | ⬜ |
| PAY-10 | Blind cash count | ⬜ |
| PAY-11 | *Fecho de caixa* with variance reporting | ⬜ |
| PAY-12 | Meal-voucher tenders (Ticket Restaurante, Edenred, Sodexo) | ⬜ |
| PAY-13 | MB WAY / Multibanco references via Ifthenpay or Easypay | ⏭ |
| PAY-14 | Integrated TPA (SIBS, Unicre, SumUp, myPOS) | ⏭ |

## RPT — Reporting

| ID | Task | Status |
|---|---|---|
| RPT-01 | Reporting read-model schema, isolated from transactional tables | ⬜ |
| RPT-02 | X report — mid-shift snapshot | ⬜ |
| RPT-03 | Z report — daily close, reconciling to the cent | ⬜ |
| RPT-04 | Sales by item | ⬜ |
| RPT-05 | Sales by category | ⬜ |
| RPT-06 | Sales by hour / daypart | ⬜ |
| RPT-07 | Sales by staff member | ⬜ |
| RPT-08 | VAT summary by rate | ⬜ |
| RPT-09 | Payment method breakdown | ⬜ |
| RPT-10 | Void and discount report | ⬜ |
| RPT-11 | SAF-T download from the back-office | ⬜ |
| RPT-12 | Business-day boundary handling (regional, incl. Azores) | ⬜ |

## QR — QR self-ordering

| ID | Task | Status |
|---|---|---|
| QR-01 | Per-table QR code generation | ⬜ |
| QR-02 | Public menu browsing (no account) | ⬜ |
| QR-03 | Guest cart and order submission | ⬜ |
| QR-04 | Route guest order → cloud → Site Agent | ⬜ |
| QR-05 | Staff approval before firing to kitchen | ⬜ |
| QR-06 | Order status for the guest | ⬜ |
| QR-07 | Allergen and dietary filtering | ⬜ |
| QR-08 | Multi-language guest UI (pt / en / es / fr) | ⬜ |
| QR-09 | Guest payment | ⏭ |

## QA — Automated testing

> See [../development/e2e-testing.md](../development/e2e-testing.md) for the
> harness itself and an honest account of what QA-04/06/07/08 are still
> blocked on.

| ID | Task | Status |
|---|---|---|
| QA-01 | Choose an E2E framework (Playwright vs alternatives) | ✅ Playwright + TypeScript, `src/web/e2e` |
| QA-02 | E2E harness — app + Postgres + seeded tenant | 🚧 `webServer` starts API + `pos` fresh each run; database is the persistent dev instance, not disposable per run |
| QA-03 | Deterministic test data builders | ✅ `tests/support/api.ts` — looks menu items up by name, never by id |
| QA-04 | Fixed-clock control for time-dependent tests | ⬜ nothing built yet needs it — see e2e-testing.md |
| QA-05 | E2E: full service loop, seat → order → fire → pay → close | ✅ `walking-skeleton.spec.ts` — open → order → split → close → receipt, driving the real UI. "Fire" and "pay" aren't built yet (KIT/PAY), so the loop ends at close |
| QA-06 | E2E: offline mode — network killed mid-service | ⬜ blocked on WEB-04/SYN — no offline capability exists to test |
| QA-07 | E2E: split-bill flows | 🚧 even split covered (`split-preview.spec.ts`, API-level, sweeps 1/2/3/5/7 ways); by-item/by-cover blocked on ORD-16/17 |
| QA-08 | E2E: multi-terminal concurrency | ⬜ blocked on ORD-21 |
| QA-09 | Testcontainers integration-test base fixture | ✅ `TenantIsolationIntegrationTests` — real disposable Postgres per run, migrates for real, creates `brasa_app` the same way `initdb` does. One fixture so far; extract a shared base once a second test needs it |
| QA-10 | Tenant isolation test suite (RLS) | ✅ Automated version of the manual verification that caught ADR 0010: zero rows with no/wrong tenant set, own rows only with the right one, DDL refused — queried as `brasa_app` via raw SQL, deliberately bypassing the EF convenience filter so a silently-disabled RLS policy can't hide behind it |
| QA-11 | Idempotency replay test harness | ✅ `idempotency.spec.ts` — a mutating request replayed 3× with the same `Idempotency-Key` returns byte-identical responses (`Idempotent-Replay: true` on replays 2/3), and the underlying side effect runs exactly once: `POST /orders` replayed never creates a second order for the table, `POST /orders/{id}/close` replayed never issues a second fiscal document (the exact scenario `IdempotencyMiddleware`'s own doc comment calls out — CLAUDE.md hard rule 3). Also proves the negative cases: a *different* key against the same now-occupied table is a genuine 409, not a cache hit, and a missing key 400s (`request.idempotency_key_required`) |
| QA-12 | Fiscal golden-file infrastructure | ⬜ |
| QA-13 | Load test — 50 sites × 5 terminals at service rates | ⬜ |
| QA-14 | Accessibility checks on POS and guest UIs | ✅ `pos` only (no guest UI yet — `order`/QR is post-I8) — `accessibility.spec.ts`, axe-core against WCAG 2.0/2.1 A+AA. Found and fixed 5 real color-contrast failures on first run, not suppressed |

## OPS — Infrastructure, CI, observability

| ID | Task | Status |
|---|---|---|
| OPS-01 | Docker Compose — PostgreSQL 18 (ICU pt-PT) + Seq | ✅ |
| OPS-02 | CI — build gate, tests, vulnerability scan | ✅ |
| OPS-03 | CI — documentation link checking | ✅ |
| OPS-04 | Docs site published to GitHub Pages | ✅ |
| OPS-05 | `.gitattributes` line-ending normalisation | ✅ |
| OPS-06 | Issue and PR templates | ✅ |
| OPS-07 | Structured logging with tenant / site / terminal enrichment | ⬜ |
| OPS-08 | OpenTelemetry traces and metrics | ⬜ |
| OPS-09 | Health and readiness probes including the database | ✅ `GET /health` (liveness, no dependencies) / `GET /health/ready` (PostgreSQL reachability, `DatabaseHealthCheck`). Verified live: healthy with DB up, `503` with the container stopped, recovers once it's back |
| OPS-10 | Hangfire setup and dashboard | ⬜ |
| OPS-11 | Production deployment (Hetzner + Caddy) | ⬜ |
| OPS-12 | Automated database backup and a tested restore drill | ⬜ |
| OPS-13 | Secret management | ⬜ |
| OPS-14 | Error tracking (Sentry) for web clients | ⬜ |
| OPS-15 | Uptime and SAF-T submission alerting | ⬜ |
| OPS-16 | Staging environment | ⬜ |

## DOC — Documentation system

| ID | Task | Status |
|---|---|---|
| DOC-01 | Architecture overview and three-tier design | ✅ |
| DOC-02 | Fiscal reference — ATCUD, signature, QR, SAF-T | ✅ |
| DOC-03 | ADRs 0001–0008 with an index | ✅ |
| DOC-04 | AI session brief + repo map | ✅ |
| DOC-05 | Glossary of Portuguese fiscal and restaurant terms | ✅ |
| DOC-06 | Documentation contract and PR checklist | ✅ |
| DOC-07 | Feature page template | ✅ |
| DOC-08 | API contract for multi-platform clients | ✅ |
| DOC-09 | Backlog and progress tracking (this page) | ✅ |
| DOC-10 | Per-feature pages, written as features land | 🚧 |

## MOB — Mobile apps

> Post-launch. The backend seams (API-01…18, IDN-03…07) are what make these
> require **no backend change**.

| ID | Task | Status |
|---|---|---|
| MOB-01 | Choose the mobile stack | ⬜ |
| MOB-02 | Generate the platform API client from OpenAPI | ⬜ |
| MOB-03 | Staff handheld — ordering | ⬜ |
| MOB-04 | Staff handheld — offline engine | ⬜ |
| MOB-05 | Staff handheld — LAN discovery of the Site Agent | ⬜ |
| MOB-06 | Owner dashboard app | ⬜ |
| MOB-07 | Customer app — menu, ordering, loyalty | ⬜ |
| MOB-08 | Native KDS app | ⬜ |
| MOB-09 | APNs push adapter | ⬜ |
| MOB-10 | FCM push adapter | ⬜ |
| MOB-11 | Deep linking / universal links | ⬜ |
| MOB-12 | App Store and Play Store release pipelines | ⬜ |

## DIF — Differentiators

> Rationale, market analysis and validation status:
> **[differentiation.md](differentiation.md)**. Nothing here should be built
> before it is validated with real restaurants.

| ID | Task | Status |
|---|---|---|
| DIF-01 | Accountant portal — read-only tenant access for the *contabilista* | ⬜ |
| DIF-02 | Automated SAF-T delivery direct to the accountant | ⬜ |
| DIF-03 | Compliance dashboard — what is filed, what is missing, what is due | ⬜ |
| DIF-04 | VAT rate change auto-application via effective dates | ⬜ |
| DIF-05 | Migration importer — Zone Soft | ⬜ |
| DIF-06 | Migration importer — WinRest | ⬜ |
| DIF-07 | Parallel-run mode alongside an incumbent system | ⬜ |
| DIF-08 | Recipe and ingredient costing | ⬜ |
| DIF-09 | Live margin per dish | ⬜ |
| DIF-10 | Supplier price tracking with margin alerts | ⬜ |
| DIF-11 | True margin by channel, including aggregator commission | ⬜ |
| DIF-12 | Menu engineering classification (star / plowhorse / puzzle / dog) | ⬜ |
| DIF-13 | Void and discount anomaly detection (shrinkage) | ⬜ |
| DIF-14 | Demand forecasting for prep quantities | ⬜ |
| DIF-15 | Staff scheduling driven by forecast demand | ⬜ |
| DIF-16 | Waste tracking and reporting | ⬜ |
| DIF-17 | Natural-language reporting for owners | ⬜ |
| DIF-18 | Direct reservations and waitlist (TheFork commission alternative) | ⬜ |
| DIF-19 | Offline-capability proof — a visible, demonstrable guarantee | ⬜ |
| DIF-20 | Order-entry speed as a tracked product metric | ⬜ |
| DIF-21 | Accounting integrations — Primavera, Sage, PHC, Moloni | ⬜ |
