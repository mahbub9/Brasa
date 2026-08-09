# AI session brief — start here

> **You are picking up development on this repository. Read this file
> completely before reading any source.** It is written so you can be productive
> without scanning the tree. It is maintained deliberately; if it is wrong, fix
> it in the same commit as whatever proved it wrong.

**Last verified:** 2026-08-10 · **Phase:** I0 complete except deployment (OPS-11); I1's floor plan and menu modifiers proven live end-to-end, plus menu item description/allergens (CAT-02, still 🚧 — image upload not built), course assignment per item (CAT-14) and kitchen station routing per item (CAT-15, independent tags on the same greenfield shape) and a second web client — the `admin` back-office shell (WEB-09, its own pt/en toggle) with its first real editor, menu management (WEB-10, still 🚧 — floor-plan editing not built); I2's pre-bill preview (ORD-18/19), order history/search (ORD-22), kitchen notes (ORD-06), line and order discounts (ORD-11, percentage or fixed, composing, no manager-authorisation gate yet), voiding a line (ORD-10, still 🚧 — same no-authorisation-gate-yet shape, its own row title names manager authorisation as in scope), table transfer (ORD-12), line transfer (ORD-13), order merge (ORD-14), split by item/cover (ORD-16/17) and takeaway orders (ORD-20) pulled forward and done; I3's `ETag`/304 caching on `GET /menu` (API-10), client version negotiation (`X-Brasa-Client` parsing + `GET /client-requirements` — API-06/07), RFC 8594 `Deprecation`/`Sunset` headers (API-08, a no-op until a real `/api/v2` exists), per-tenant-and-client rate limiting (API-12, a sixth `ErrorType.RateLimited` → 429), cursor pagination on `GET /orders` (API-09), Brotli/gzip response compression (API-11) and a committed OpenAPI document (API-13) pulled forward and done; the idempotency replay guarantee (API-05) now has an automated test harness (QA-11); menu bulk CSV import (CAT-17, still 🚧 — Excel not built) pulled forward from I1; every request now logs with `TenantId` attached (OPS-07, still 🚧 — doesn't yet reach the HTTP completion-summary line, a known pipeline-ordering gap not a silent one); the two deep-link verification documents exist too (API-18, honestly empty — no bundle id/package name exists to put in either until a native app does); real distributed traces and metrics now exist too (OPS-08, OTLP-exported to Seq), after finding Seq itself had been silently crash-looping (fixed, see §7); `SplitByItem` (ORD-16) was made discount/void-aware, closing a gap ORD-11/ORD-10 had each explicitly left open; the first feature-docs pages exist (DOC-10 — `docs/features/{discounts,void-a-line,menu-item-classification}.md`); and `pos`'s server error messages are now localized by error code (closes the "Server-sent error text" gap ADR 0011 named), not just the raw English `ProblemDetails.title`

---

## 1. What this is, in sixty seconds

**Brasa** — Portuguese for the glowing embers of a grill, after *frango na
brasa*. Every assembly, namespace and container is named `Brasa.*`.

A multi-tenant restaurant management SaaS for **Portugal**, built by a **solo
developer**, targeting a first live restaurant in **~6 months**.

The dominant constraint is not product features — it is **Portuguese fiscal
law**. Invoicing software used in Portugal must be certified in advance by the
Autoridade Tributária (AT) under Portaria 363/2010. Fines for non-certified
software run €3,000–€18,750 per infraction. Certification is granted to the
software *producer*, so it gates the ability to **sell**, not to **build**.

Three requirements collide and shape everything:

1. A restaurant's internet **will** fail; service cannot stop.
2. Fiscal documents need a **chained RSA signature over a gapless per-series
   sequence**, so signing must work offline — but a browser cannot hold a key.
3. A PWA **cannot open raw TCP sockets**, so it cannot drive ESC/POS printers.

All three are solved by the **Site Agent**: a .NET worker running inside each
restaurant. This is the single most consequential design decision here. If you
understand only one thing, understand that.

## 2. Hard invariants — never violate these

| # | Rule | Why |
|---|---|---|
| 1 | Money is `Money` (integer minor units). Never `double`/`float`/bare `decimal` | Totals must reconcile to the cent against SAF-T and the Z report |
| 2 | Split money with `Allocate`, never division | Dividing €10 three ways loses a cent |
| 3 | Never call `DateTime.UtcNow`. Inject `IClock` | Fiscal `SystemEntryDate` must be monotonic per series; the chain must be testable |
| 4 | Never mutate an issued fiscal document | Corrections are credit notes. An invisible-alteration path is a **certification failure** |
| 5 | Modules never reference or query each other | Use integration events. This is what keeps later extraction from being a rewrite |
| 6 | Expected failures return `Result`, not exceptions | Exceptions are for genuine faults only |
| 7 | Never weaken `TreatWarningsAsErrors` | Suppress in `.editorconfig` **with a written reason**, or fix it |
| 8 | Signature chaining is **per-series**, never global | Two series advance independently |
| 9 | **No cookie auth. No web-only assumptions in the API** | Android and iOS ship soon after web and must need *zero* backend change |
| 10 | Every realtime message must have a **REST equivalent** | A platform with no usable SignalR client must still work, degraded but correct |
| 11 | Error codes are a **public contract** — once released, the meaning never changes | Mobile clients branch on them and cannot be patched quickly. Enforced, not just stated: [error-codes.md](../architecture/error-codes.md) + `ErrorCodeRegistryTests` (API-04) |

## 3. Where things stand

**Authoritative inventory: [../product/status.md](../product/status.md).** Read
it — it marks every empty project explicitly, because a scaffold makes empty
things look finished.

Condensed:

- ✅ **Built, tested, and proven live** (not just unit-tested — see §3a):
  `Money` (17 tests), `Result`/`Error` (18th test: the error-code registry,
  API-04 — see [error-codes.md](../architecture/error-codes.md)),
  `PortugueseTimeZone` (14 tests — IANA ids resolve on this runtime, Azores
  stays 1h behind the mainland year-round, the same instant can land on two
  different business days in different regions, the exact scenario the
  type's own doc comment warns about), `Result`/`Error`/`ErrorMapping`
  (23 tests across `Brasa.Shared.Tests` and `Brasa.Api.IntegrationTests` —
  `Value` on a failed `Result<T>` throws with the error code named, and all
  6 `ErrorType`→HTTP status mappings are pinned directly rather than only
  through whichever status each endpoint's own tests happen to trigger),
  tenancy +
  **real RLS** (DAT-01…06),
  `Catalog` (categories/items, seeded, soft delete — CAT-18, modifier groups
  — CAT-03/04, description + declared allergens — CAT-02, a fixed EU-wide
  taxonomy, not a rate awaiting an accountant's confirmation like `VatRate`,
  bulk CSV import — CAT-17, still 🚧, Excel not built, rows import
  independently so one bad row doesn't fail the file),
  `Ordering` (open against a real table/add-line-with-modifiers
  — ORD-05/per-line kitchen notes — ORD-06/line and order discounts,
  percentage or fixed, composing — ORD-11, no manager-authorisation gate
  yet (IDN-11)/transfer to a different table —
  ORD-12/transfer a single line to a different order — ORD-13/merge two
  orders — ORD-14, new `OrderStatus.Merged`, no migration needed/split
  evenly, by item or by cover — ORD-15/16/17/takeaway order with no table —
  ORD-20, `IsTakeaway`/pre-bill preview — ORD-18/19, provably non-fiscal,
  see §7/close/history-search — ORD-22), `Floor` (rooms, tables, full `Free ⇄ Occupied ⇄ Dirty ⇄ Free`
  lifecycle, `xmin` optimistic concurrency — FLR-01/02/04), `Fiscal` contract
  + `Fiscal.Mock`, API layer (versioning, ProblemDetails, idempotency, CORS,
  the full order flow composing all four modules), the `pos` web shell
  (React 19 + Vite + TS, table-picker → order incl. a modifier picker →
  receipt, WEB-01/05, pt-PT default / en toggle behind a mobile-portable
  cookie seam — WEB-13, ADR 0011), the `admin` back-office shell (WEB-09 —
  React 19 + Vite + TS on port 5174, own full pt/en toggle sharing `pos`'s
  `brasa.lang` cookie, genuinely English in English mode since not every
  staff member is a Portuguese speaker) with its first real editor
  (WEB-10's menu slice — toggle category visibility, 86/reprice/delete an
  item, bulk-import more via CAT-17's CSV pipeline, all backed by a new
  `GET /menu/all` that deliberately doesn't filter the way the guest-facing
  `GET /menu` does; floor-plan editing, FLR-03, isn't built), a Playwright
  E2E harness driving the real
  UI (`src/web/e2e`, QA-01/03/05/14 incl. axe-core accessibility scans, 87
  tests green on a clean run — the seeded floor plan was doubled to 16
  tables after back-to-back full runs started exhausting the original 8, a
  QA-02 scaling limitation, not a product bug; see
  [e2e-testing.md](../development/e2e-testing.md)), an idempotency replay
  harness (QA-11 — a mutating request replayed 3× with the same
  `Idempotency-Key` is byte-identical and runs its side effect exactly
  once; specifically proves a retried `POST /orders/{id}/close` never
  issues a second fiscal document, the scenario `IdempotencyMiddleware`'s
  own doc comment names),
  `Brasa.Api.IntegrationTests` (DAT-11,
  QA-09/10 — real Testcontainers Postgres proving tenant isolation by
  automated test, not just manual psql anymore), a liveness/readiness split
  (`/health`, `/health/ready` — OPS-09, live-verified against a stopped and
  restarted PostgreSQL container), `ETag`/`If-None-Match` caching on
  `GET /menu` (API-10 — deliberately not `GET /floor`, whose state changes
  too continuously for caching to pay off; live-verified 200→304, and the
  repeated-run E2E discipline caught a real bug where the helper's own JSON
  serialization used PascalCase instead of the app's configured camelCase,
  see [status.md](../product/status.md)), client version negotiation
  (API-06/07 — best-effort `X-Brasa-Client` header parsing that enriches
  every log line for the request via Serilog's `LogContext`, plus
  `GET /client-requirements` looking up the caller's client id in a
  config-bound policy; ships ahead of any client that sends the header or
  calls the endpoint yet), RFC 8594 `Deprecation`/`Sunset` response headers
  (API-08 — config-bound under `Api:Deprecation`, empty by default, so a
  no-op until a real `/api/v2` gives it something to announce), rate
  limiting per `(tenant, X-Brasa-Client client id)` on `/api/**` (API-12 —
  a real, generous production default (1000 req/60s) plus a much higher
  dev-only override, because every dev/E2E client shares one bucket per
  tenant until a client actually sends the header; a sixth `ErrorType`,
  `RateLimited` → 429, joined the five `ErrorMappingTests` already pinned),
  cursor pagination on `GET /orders` (API-09 —
  the one genuinely unbounded collection today; additive via a new
  `X-Next-Cursor` response header, not a breaking change to the
  already-shipped body shape), Brotli/gzip response compression incl.
  `application/problem+json` error bodies (API-11 — safe over HTTPS here
  since there's no cookie-reflected secret for BREACH to exploit, ADR 0008),
  the `BillRequested` floor-plan signal (FLR-04 — `POST /tables/{id}/request-bill`
  plus a "Pedir conta" button; the CSS and i18n for it existed before the
  endpoint did, see §7), 86-ing a menu item (CAT-13 — `MarkAvailable`/
  `MarkUnavailable` and `AddLine`'s guard for both existed since I0 with no
  endpoint to reach them, the same shape as FLR-04) and menu item
  repricing (CAT-19, newly minted — `MenuItem.Reprice` and its
  negative-price guard existed since I0 too, a third instance of the same
  pattern; verified live that an already-open order's line survives a
  reprice unchanged, not just that the flag flips), and menu category
  visibility (CAT-01 — a fourth instance one level up: `MenuCategory.IsVisible`
  had no setter at all, despite the row's own title naming "visibility"
  and being marked done; hiding a category now removes it and every item
  under it from `GET /menu` in one call), a committed OpenAPI
  document ([docs/openapi/v1.json](../openapi/v1.json),
  API-13 — regenerated by hand for now; CI drift-checking is the separate,
  not-yet-built API-14), Docker Compose
  (PostgreSQL 18 + Seq), full docs tree, CI (including an `e2e` job —
  written, not yet run in CI).
- 📁 **Empty projects (structure only, zero logic):** `Modules.Identity`,
  `Modules.Payments`, `Modules.Reporting`, `Fiscal.Portugal`.
- 🚧 **Stub:** `SiteAgent` starts and stops; nothing else.
- ⬜ **Not started:** `kds`/`order` web clients, deployment.

**Delivery is incremental** — vertical slices, each ending in a runnable demo.
**[../product/roadmap.md](../product/roadmap.md) says what to build next**;
[../product/backlog.md](../product/backlog.md) holds the 292 tasks and their
status. Reference IDs in commits: `feat(identity): terminal pairing (IDN-07)`,
and update the status in the same commit.

**Current increment: I0 is done except deployment (OPS-11).** I1 ("Menu and
floor," see roadmap) is well underway — floor plan (FLR-01/02/04, WEB-05),
modifiers (CAT-03/04), the `admin` back-office shell (WEB-09) and its menu
editor (WEB-10's menu slice) are done and proven; price lists (CAT-05), the
floor-plan editor (FLR-03) and staff/reporting screens (WEB-11) are not.

Backend/I0 tasks — **done**: DAT-01/03/04/**05**/06/**11**/10 · API-01/03/05 ·
CAT-**01**/02/03/04/07/**13**/**17**/18/**19** ·
ORD-01/02/03/04/**05**/**06**/**12**/**13**/**14**/15/**16**/**17**/**18**/**19**/**20**/**22** ·
FIS-01/02/03 · WEB-01/05/13 · QA-01/03/05/**09**/**10**/**11**/**14** · FLR-01/02/**04** ·
API-**04**/**06**/**07**/**08**/**09**/**10**/**11**/**12**/**13**/**18** · OPS-**09**. API-14 is 🚧 (drift detection, not semantic breaking-change detection).

**Not in I0:** auth, offline, printing, real fiscal, menu editing, KDS.

> RLS (DAT-05), idempotency (API-05) and `/api/v1` (API-01) were in I0 **on
> purpose**, and it was the right call: RLS in particular turned out to be
> silently broken (§3a) in a way that would have been far more expensive to
> discover after other modules copied the same pattern.

### 3a. Verified live — and what that caught

I0's backend was driven end-to-end against a real API process and a real
PostgreSQL container, not only against unit tests: open a table → mixed
food+alcohol order → even 3-way split → close → fiscal document, plus an
idempotency replay checked directly against the database. Full script and
numbers: [../product/status.md](../product/status.md#i0-demo-verified-live-not-just-unit-tested).

This surfaced three real bugs that `dotnet build` and the pre-existing unit
tests both missed:

1. **RLS was inert.** The bootstrap Postgres role is a superuser; superusers
   bypass RLS unconditionally, `FORCE ROW LEVEL SECURITY` notwithstanding. Now
   [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md).
2. **New order lines were tracked `Modified`, not `Added`.** EF Core's `Guid`
   key convention assumes a non-default key means "already exists." Fixed with
   `ValueGeneratedNever()` in `ApplyEntityConventions`.
3. **VAT was computed backwards.** Menu prices are VAT-inclusive under
   Portuguese law; the fiscal document must derive net/VAT from gross, not add
   VAT on top. See §7 and `docs/fiscal/README.md`.
4. **`Table.Occupy()` had no database-level concurrency guard.** Two
   concurrent "open this table" requests could both read `Free`, both
   transition in memory, and both successfully save — EF's default
   `SaveChangesAsync` is a blind `UPDATE ... WHERE Id = @id` with nothing to
   notice a second writer got there first. Found by E2E flakiness under
   Playwright's 2-worker parallelism, not by inspection. Fixed with an
   `xmin`-based concurrency token (`TableConfiguration.cs`) — see §7 and
   [status.md](../product/status.md#a-real-concurrency-bug-found-by-running-the-suite-enough-times).

**The lesson, not just the fix:** a clean build and green unit tests proved
nothing about whether tenant isolation actually worked, and a single passing
E2E run proved nothing about whether concurrent access was safe. If you build
something that depends on database-level behaviour (RLS, triggers,
constraints, concurrent writes), run it against the real database, under
real concurrent load, and try to break it before calling it done.

The `pos` web shell was first verified only at the wire level — `curl`
reproducing the exact browser request sequence (CORS preflight, `Origin`
header, `Idempotency-Key`) against the running API — because no
browser-automation tool was available in that session. That gap is now
closed: a Playwright harness (`src/web/e2e`, QA-01/03/05) drives the actual
rendered UI in a real Chromium instance and passes, verified both against
already-running dev servers and from a hard cold start (both processes
killed, Playwright's own `webServer` config launching them from nothing). See
[../development/e2e-testing.md](../development/e2e-testing.md). What's
**still** unverified: the new `e2e` CI job itself — written, mirrors what
passed locally, but no push has exercised it in actual GitHub Actions yet.

## 4. Repo map

Detailed file-by-file inventory: [repo-map.md](repo-map.md).

```
src/backend/
  Brasa.Api               Cloud API. Minimal APIs under /api/v1
  Brasa.Shared            Shared kernel — depends on no module
  Brasa.Modules.Identity  Users, roles, staff PINs, terminal pairing
  Brasa.Modules.Catalog   Menu, modifiers, price lists, tax rules
  Brasa.Modules.Ordering  Orders, courses, splits, transfers
  Brasa.Modules.Floor     Rooms, tables, table state
  Brasa.Modules.Fiscal    IFiscalProvider, document lifecycle, audit
  Brasa.Modules.Payments  Tenders, cash sessions, tips
  Brasa.Modules.Reporting Read models, X/Z reports, VAT summaries
  Brasa.Fiscal.Portugal   ATCUD, RSA chain, QR, SAF-T, AT webservices
  Brasa.Fiscal.Mock       Deterministic fake for dev and tests
src/agent/
  Brasa.SiteAgent         In-restaurant worker: signing, printing, LAN hub
src/web/
  pos                     POS PWA (React + TS + Vite) — I0 shell, WEB-01
  admin                   Back-office SPA (React + TS + Vite) — shell + menu editor, WEB-09/10
  e2e                     Playwright E2E harness — QA-01/03/05
tests/                            Unit, fiscal golden-file, integration
docs/                             This documentation tree
infra/                            docker-compose (PostgreSQL 18, Seq)
```

## 5. Task → where to look

| Doing this | Read |
|---|---|
| Anything fiscal | [../fiscal/README.md](../fiscal/README.md) — legal constraints, not preferences |
| Money, totals, VAT, splitting | [../architecture/money.md](../architecture/money.md) |
| Adding or changing **any endpoint** | [../architecture/api-contract.md](../architecture/api-contract.md) — the mobile-readiness rules |
| Adding a module, or a cross-module call | [../architecture/module-boundaries.md](../architecture/module-boundaries.md) |
| Tenant-scoped data, RLS | [../architecture/multi-tenancy.md](../architecture/multi-tenancy.md) |
| Offline, printing, the agent | [../architecture/site-agent.md](../architecture/site-agent.md) |
| "Why is it built like this?" | [../architecture/decisions/](../architecture/decisions/) |
| Code style, analyzer policy | [../architecture/conventions.md](../architecture/conventions.md) |
| Testing expectations | [../development/testing.md](../development/testing.md) |
| A Portuguese term you don't know | [../glossary.md](../glossary.md) |
| **What to build next** | [../product/roadmap.md](../product/roadmap.md) — increments and demo scripts |
| Task status, or a task's ID | [../product/backlog.md](../product/backlog.md) |
| Why this product is worth building | [../product/differentiation.md](../product/differentiation.md) |
| End-to-end testing | [../development/e2e-testing.md](../development/e2e-testing.md) |
| The overall plan and roadmap | [../product/plan.md](../product/plan.md) |

## 6. Decisions already made — do not re-litigate

Each has an ADR with a **"Revisit when"** section. Reopen only if a trigger
there is actually met.

| ADR | Decision |
|---|---|
| [0001](../architecture/decisions/0001-modular-monolith.md) | Modular monolith, not microservices |
| [0002](../architecture/decisions/0002-own-fiscal-engine.md) | Build our own AT-certified engine, not a partner API |
| [0003](../architecture/decisions/0003-site-agent.md) | In-restaurant Site Agent |
| [0004](../architecture/decisions/0004-react-pwa-not-blazor.md) | React PWA clients, **not Blazor** (despite a C# backend) |
| [0005](../architecture/decisions/0005-plain-guid-ids.md) | Plain `Guid` ids; isolation enforced by RLS |
| [0006](../architecture/decisions/0006-no-mediatr.md) | Hand-rolled dispatcher; MediatR is now commercially licensed |
| [0007](../architecture/decisions/0007-client-agnostic-api.md) | One client-agnostic API for every platform — **no BFF** |
| [0008](../architecture/decisions/0008-token-auth-no-cookies.md) | Token auth with PKCE, device-bound refresh, **no cookies** |
| [0009](../architecture/decisions/0009-incremental-delivery.md) | Incremental delivery; walking skeleton first, demo script is done |
| [0010](../architecture/decisions/0010-rls-runtime-role-split.md) | Split the DB role: unprivileged at runtime, superuser only for migrations |
| [0011](../architecture/decisions/0011-i18n.md) | i18next, pt default, cookie preference behind a storage seam swappable for mobile |

## 7. Traps — things that look wrong but are intentional

- **Order lines copy the item name, price and VAT rate.** That is correctness,
  not denormalisation. A receipt must show what the item cost *when it was sold*.
- **The pre-bill given to a table is a *documento não fiscal*.** Issuing it as an
  invoice would fiscalise every table that merely asks to see the bill.
  `GET /orders/{id}/pre-bill` (ORD-18/19) enforces this by construction, not
  just by convention: `PreBillDto` has no document number, ATCUD or QR field
  at all, and the handler never calls `IFiscalProvider` — it reuses
  `FiscalDocumentLine`'s gross→net/VAT math purely as a calculator. Because
  nothing is persisted or numbered, requesting it any number of times against
  an unchanged order reproduces identical figures (the "reprint" requirement,
  ORD-19) for free — verified live in `pre-bill.spec.ts`, not just asserted by
  the type shape.
- **A discount (ORD-11) never touches `OrderLine.UnitPrice`.** It would be
  tempting to apply a discount by reducing the unit price handed to
  `FiscalDocumentLine`, but that price is a snapshot taken at add-time and
  must stay exactly what the guest was charged when the line was rung up —
  the same "order lines copy the price" rule above. Instead
  `OrderEndpoints.BuildFiscalLines` renders a discount as its own **separate
  negative** `FiscalDocumentLine` (`"Desconto: {ItemName}"`) at the
  discounted line's own VAT rate. This also sidesteps a real correctness
  trap: a line-level discount doesn't divide evenly across a multi-quantity
  line (`Money` deliberately has no division operator — see
  [money.md](../architecture/money.md)), so trying to fold it into a
  per-unit price for a `quantity > 1` line would either lose a cent or need
  `Allocate`'s own remainder logic for no reason. An order-level discount is
  prorated across lines by `Money.Allocate` (the same tool `SplitByCover`
  uses) before being folded into that same per-line discount entry — so
  `order.Total` and `document.GrossTotal` reconcile to the cent by
  construction, not because the two code paths happened to agree.
- **VAT rates are data with effective dates, not constants.** They are unconfirmed
  by an accountant and politically contested. Never hardcode them.
- **Menu prices are VAT-inclusive (gross), not net.** `MenuItem.Price` and
  `OrderLine.UnitPrice` are the final amount the guest pays. A fiscal document
  *derives* net and VAT from the gross price — computing it the other way
  round makes the running order total disagree with what the fiscal document
  charges. See `docs/fiscal/README.md`.
- **The Azores are an hour behind the mainland.** This moves the daily close and
  SAF-T period boundaries. `PortugueseRegion` exists for this.
- **`pos`'s `brasa.lang` cookie is not the ADR 0008 cookie.** ADR 0008 bans
  *authentication* cookies. `brasa.lang` is a client-only UI preference the
  API never sends or reads — setting it does not weaken that rule. Don't
  "fix" it into `localStorage` on sight; the user explicitly asked for a
  cookie. See [ADR 0011](../architecture/decisions/0011-i18n.md).
- **`formatMoney` and the receipt's issued date are hardcoded `'pt-PT'`,
  even in English mode.** Not a missed i18n string — a total or a fiscal
  timestamp must not change format because staff switched their own
  interface language. See ADR 0011.
- **Table labels ("Mesa 1") are the one seeded-data string that *does*
  translate, unlike menu item names.** Real staff here include people who
  don't read Portuguese, and "Mesa" is a generic word, not an identity-
  bearing name the way a dish's is — `src/lib/tableLabel.ts` renders it as
  "Table 1" in English (display-only; `Table.Label` itself is untouched).
  Don't assume every seeded string follows the menu-item precedent of
  staying untranslated — check whether it's actually content, or just a
  generic label wearing a Portuguese word. See ADR 0011.
- **`OpenOrderAsync`/`CloseOrderAsync` save two `DbContext`s sequentially, not
  in one transaction — and each saves them in the OPPOSITE order on purpose.**
  `OpenOrderAsync` saves Floor first: that's the row with the new `xmin`
  concurrency check, so a lost race is caught *before* an `Order` exists,
  leaving nothing to clean up. `CloseOrderAsync` saves Ordering first: the
  closed-and-fiscally-issued order is the part that must never be silently
  lost, so marking the table `Dirty` afterward is deliberately best-effort.
  Don't "fix" either into a `TransactionScope` across two Npgsql connections,
  and don't assume the other handler's ordering applies here too — read the
  comment at each call site, and see
  [module-boundaries.md](../architecture/module-boundaries.md) rule 5.
- **`Table` has an `xmin`-based optimistic concurrency token, and its
  migration's `Up()`/`Down()` are deliberately empty.** `xmin` is a
  PostgreSQL system column every row already has — `dotnet ef migrations add`
  scaffolds an `ADD COLUMN xmin`, which Postgres rejects outright (the name is
  reserved). The migration exists only so EF's model snapshot knows about the
  shadow property; there is no DDL to run. If you regenerate this migration,
  strip the `AddColumn`/`DropColumn` calls again.
- **`Order.TableId` is a bare `Guid`, not a navigation property, and
  Ordering never queries `floor.tables`.** Same pattern as
  `OrderLine.MenuItemId` — a cross-module reference is an opaque id an
  endpoint resolves, never a join. `TableLabel` is the part that gets
  snapshotted (for the receipt-history reason above); `TableId` deliberately
  isn't.
- **A takeaway order's `TableId` is `Guid.Empty` (ORD-20) — never check that
  directly.** Check `Order.IsTakeaway` instead. `Guid.Empty` is not a magic
  value anywhere else in this codebase; it means exactly one thing here
  ("this order was opened with `OpenTakeaway`, so there is no Floor table to
  look up"), and `TransferOrderAsync`'s existing "table might not exist"
  handling already treats it correctly by coincidence — don't add a special
  case that assumes otherwise. `TransferToTable` clears `IsTakeaway` back to
  `false` when a takeaway order lands on a real table; nothing ever sets it
  the other way.
- **The seeded floor plan has 16 tables (doubled from 8 — see below), and the
  dev database is not reset between E2E runs.** Every spec that opens a table
  (`src/web/e2e`) must close the order and clear the table before finishing,
  or repeated runs exhaust the free-table pool — and because table state is
  real contended state now (the `xmin` token above), specs pick a table via
  `openOrderOnAnyFreeTable` / `openAnyFreeTable`, which retry on a 409
  instead of assuming the first "free" table they see is still free by the
  time the request lands. See `tests/support/api.ts` and `tests/support/ui.ts`.
  Even at 16, back-to-back full runs with no pause can still occasionally
  exhaust the pool once the suite is large enough — a QA-02 scaling
  limitation (the dev database isn't disposable per run), not a product bug.
  If a run fails with "No free table available", check `GET
  /orders?status=Open` for a leftover order, close it, and `POST
  /tables/{id}/clear` any table stuck `Dirty`.
- **Any endpoint that serializes JSON itself (bypassing `Results.Ok`/
  `Results.Json`) must resolve `IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>`
  and use its `SerializerOptions` — never call `JsonSerializer` with no
  options.** `Results.Ok(dto)` uses the app's configured options, which are
  camelCase; `JsonSerializer.Serialize(dto)` with no options defaults to
  `System.Text.Json`'s own PascalCase. `ETagResults.OkWithETag` (API-10) got
  this wrong on the first pass — it built silently, passed every backend
  test, and only broke visibly when `pos`'s menu screen crashed on
  `category.items` being `undefined`. See `ETagResults.cs` and the API-10
  entry in [status.md](../product/status.md).
- **An E2E test that filters `GET /orders` by a time window (`openedFrom`)
  and asserts an exact result count is not safe under `fullyParallel`.**
  Other specs create orders concurrently on the other Playwright worker, so
  a window like "everything opened since I started this test" can pick up
  rows this test didn't create — a page can legitimately come back longer
  than expected. `order-history.spec.ts`'s cursor-pagination test (API-09)
  hit exactly this: its first version asserted page lengths of 2 then 1 and
  flaked. Fixed by walking the full `X-Next-Cursor` chain and asserting
  only facts that hold regardless of concurrent noise — each of this
  test's own created order ids appears exactly once across all pages, and
  the "full page has a cursor, short page doesn't" invariant holds on every
  page — never an exact total row count.
- **`pos` never dims text with CSS `opacity` for visual hierarchy.** It looks
  fine to a sighted reviewer and quietly fails WCAG contrast anyway —
  `opacity` blends the color toward whatever's behind it, so the *effective*
  contrast is lower than the raw foreground color suggests. `accessibility.spec.ts`
  (QA-14) caught five of these on its first run. Pick a genuinely-compliant
  color instead; see [status.md](../product/status.md#accessibility-first-scan-five-real-fixes).
- **`Money.Format(culture)` is not called `ToString`.** Deliberate — it forces
  callers to name the culture, and keeps `ToString()` unambiguously invariant.
- **Unused-looking code isn't necessarily dead — check for a domain method
  or guard with no caller before assuming something is finished.** Four
  instances found the same way (grepping domain classes for public methods,
  then checking whether any endpoint actually calls them): `TablePicker.tsx`'s
  `.floor-table-BillRequested` CSS and `floor.state.BillRequested` i18n
  strings existed for a state — `Table.RequestBill()` — with *no* producer
  anywhere, so the UI could render a state that could never occur; FLR-04
  added `POST /tables/{id}/request-bill` to finally reach it. Separately,
  `MenuItem.MarkAvailable`/`MarkUnavailable` and `AddLine`'s
  `catalog.item_unavailable` guard for both had existed since I0 with no
  endpoint ever able to set `IsAvailable` to `false` — CAT-13's
  `PUT /menu/items/{id}/availability` closed that one. A third:
  `MenuItem.Reprice` and its own negative-price guard, same story again —
  CAT-19's `PUT /menu/items/{id}/price` closed it, and specifically proved
  live that the pre-existing snapshot safety (`OrderLine.UnitPrice` copied
  at add-time) actually holds under a real reprice, not just in the type
  shape. A fourth, one level up: `MenuCategory.IsVisible` had no setter *at
  all* — not even an unreachable one — despite CAT-01's own backlog title
  naming "visibility" as in scope and the row already being marked done.
  `PUT /menu/categories/{id}/visibility` closed it; hiding a category
  removes it and every item under it from `GET /menu` in one call. Before
  deleting UI/CSS/i18n/a domain method that looks unreferenced, check
  whether it's scaffolding for a state or guard that already exists and is
  already enforced, just missing the one endpoint that would reach it — the
  same way CAT-02's fields shipped ahead of the UI that would set them.
- **`GET /menu` and `GET /menu/all` are not interchangeable — `pos` must
  never call the second one.** `GET /menu` filters to visible categories
  and available items on purpose: it's what a guest may actually order.
  `GET /menu/all` (WEB-10) deliberately skips both filters, because
  `admin`'s menu editor needs to *see* a hidden category or an 86'd item to
  turn it back on — but that only works because `pos` never reaches it. If
  `pos` ever needs a second endpoint, that is a sign something about this
  split needs rethinking, not a reason to point it at `/menu/all`.
- **A test asserting `GET /menu`'s `ETag` is stable between two calls can
  break for a completely legitimate reason: another spec mutated the menu
  in the gap between them.** `menu-etag.spec.ts` assumed nothing changes
  `GET /menu`'s content between its own two back-to-back requests, true
  when written but no longer true once CAT-01/13/19 landed sibling specs
  that legitimately change the menu (category visibility, item
  availability, item price) as part of what *they* test. Under real
  parallel workers, one of those landing in the gap turns the expected
  `304` into a genuine `200` — the caching mechanism working correctly on
  content that actually changed, not a bug. Fixed by retrying the whole
  round trip (fresh `ETag`, immediate reuse) up to 5× rather than weakening
  the assertion — the same shape as the API-09 pagination test's fix for
  concurrent-spec interference under `fullyParallel`. If you add a spec
  that mutates catalog state, expect this class of interaction with
  anything that reads `GET /menu` and assumes it's stable.
- **"Sobremesas" is the seeded category with no name/item dependency
  elsewhere — but that stops being true the moment a second spec claims it
  too, and nothing warns you.** `menu-category-visibility.spec.ts` (CAT-01)
  picked it specifically because nothing else references it; adding
  `admin-menu-management.spec.ts` (WEB-10)'s own category-visibility UI
  test against the same category, without checking that comment first,
  reintroduced exactly the kind of collision CAT-01 had deliberately
  avoided — two specs racing to hide/show the same category under real
  parallel workers, each assuming exclusive ownership. Every *other*
  seeded category has a real dependency (Bebidas has "Imperial", Pratos
  Principais has "Frango na Brasa", both looked up by exact name in
  several specs; Entradas is referenced by name in four more), so there
  is no free fourth fixture — and no endpoint to create one. Fixed by
  making both specs tolerate a shared resource instead of assuming
  exclusivity: the id lookup goes through `GET /menu/all` (never
  filtered, so it can't itself fail mid-race), the hide/show/verify round
  trip retries as a whole (same shape as the `ETag` trap above), and
  WEB-10's own second visibility test is read-only precisely so it needs
  no exclusivity at all. Before adding a spec that mutates a seeded
  fixture, grep for what else already uses it — "nothing else references
  this" is a claim that can go stale.
- **`Fiscal.Mock` must never run in Production.** It produces structurally valid
  but fiscally meaningless documents.
- **The `RateLimiting` default in `appsettings.json` is tuned for production
  traffic, not for this repo's own dev/E2E traffic — they are not the same
  thing today.** `ApiRateLimiting` partitions by `(tenant, X-Brasa-Client
  client id)`, but no client sends that header yet, so every request from
  `pos`, `admin` *and* the entire Playwright suite falls into one shared
  `unknown` bucket per tenant. A first, production-shaped default (300
  req/60s) throttled the E2E suite itself — 6 unrelated specs failed with a
  real `429`, not a flake. Fixed with a much higher
  `appsettings.Development.json` override, not by weakening the production
  default to match dev traffic. If you ever see spurious `429`s running
  the suite locally, check this before assuming it's QA-02 concurrency
  flakiness (§ above) — the two look similar (an unrelated spec fails, and
  it isn't reproducible in isolation with a normal request volume) but have
  different fixes.
- **The web client gets a refresh-token cookie, but the API is not
  cookie-authenticated.** That cookie is scoped to the token endpoint only; every
  API call carries a bearer token, which is what keeps native clients working
  against an identical API.
- **A staff PIN is not a password.** It is a fast identity switch on hardware
  that was already authenticated by terminal pairing. It must never be accepted
  as a primary credential over the internet.
- **The solution file is `Brasa.slnx`**, the .NET 10 XML format — not `.sln`.
- **PostgreSQL 18 mounts `/var/lib/postgresql`**, not `.../data`. Mounting
  `.../data` makes the container refuse to start.
- **A new web client's origin must be added to `Cors:AllowedOrigins`
  (`Brasa.Api/appsettings.Development.json`) before it can call the API at
  all.** `admin` (port 5174) hit this directly: every fetch failed silently
  from the app's own perspective — no server-side error, no visible
  exception, just a hung `fetch` — because the browser blocks the response
  at the CORS layer before JS ever sees it. Only visible in the browser's
  network console, not in API logs. The list is read once at startup, not
  live-reloaded, so a config edit needs the API restarted, not just saved.
- **A test that calls `Database.MigrateAsync()` must build its `DbContext`
  through that module's design-time factory (`new XyzDbContextFactory().CreateDbContext([])`),
  never a hand-rolled `DbContextOptionsBuilder`.** `dotnet ef migrations
  has-pending-model-changes` (the design-time check) always goes through
  the factory, so it stays clean no matter how a test builds its own
  context — but `MigrateAsync` throws `PendingModelChangesWarning` as a
  hard error the moment a test's *live* model disagrees with the last
  migration's committed snapshot, and a hand-rolled options builder can
  silently drift from the factory in ways that don't show up in a source
  diff. Found via `TenantIsolationIntegrationTests`: CAT-14 hit this, was
  "fixed" by matching one visible setting (`MigrationsHistoryTable`) the
  hand-rolled builder was missing — which genuinely fixed *that* instance,
  but CAT-15's migration hit the identical error again immediately after,
  with `has-pending-model-changes` still reporting clean throughout. The
  real fix was routing the test through `CatalogDbContextFactory` itself
  (with `BRASA_MIGRATIONS_CONNECTION` pointed at the test's Testcontainers
  instance), which removes the category of bug entirely — there is no
  second configuration left to keep in sync, whatever the exact trigger
  turns out to be for the next new column. If a second module (Ordering,
  Floor) ever gets a similar Testcontainers test, build its context via
  its own design-time factory from the start.
- **`Seq:latest` needs `SEQ_FIRSTRUN_NOAUTHENTICATION` or it crash-loops.**
  A newer Seq release started requiring an explicit first-run admin
  password (or this opt-out) and refuses to start without one.
  `docker compose ps` / `docker ps` shows it as `Up` for a few seconds
  after every restart, which reads as healthy at a glance — it isn't;
  check `docker logs brasa-seq` or actually query `http://localhost:5341/api`
  if logs/traces seem to be going nowhere. Fixed in `infra/docker-compose.yml`;
  if this container is ever recreated from a different compose file (or the
  env var gets dropped), it will silently start failing this way again.
- **OpenTelemetry's `AddOtlpExporter` does not append a per-signal OTLP
  path to `Endpoint` — you must.** `otlp.Endpoint = new Uri("http://host:5341/ingest/otlp")`
  posts to exactly that URL and 404s against Seq (which expects
  `/ingest/otlp/v1/traces`, `/ingest/otlp/v1/metrics`, etc.). The
  auto-append-per-signal-path behaviour only exists on the separate,
  newer unified `UseOtlpExporter()` helper (which reads
  `OTEL_EXPORTER_OTLP_ENDPOINT`), not on the per-signal
  `TracerProviderBuilder`/`MeterProviderBuilder.AddOtlpExporter(...)`
  extension `Program.cs` actually uses. A failed export throws **no
  exception anywhere in the app** — it's an OpenTelemetry SDK-internal
  concern by design — so this is invisible without either checking the
  destination's own ingestion logs or temporarily subscribing a raw
  `System.Diagnostics.ActivityListener` to inspect the exporter's own
  outbound `System.Net.Http.HttpRequestOut` spans directly (which is how
  this was actually found — `dotnet ef`-style "no error" is not the same
  as "it worked"). Fixed by appending `/v1/traces`/`/v1/metrics`
  explicitly in `Program.cs`.
- **`Order.Close()`'s "at least one line" guard counts voided lines too —
  the real "can this order actually close" answer lives one layer up, in
  Fiscal.** Voiding (ORD-10) every line on an order still satisfies
  `Order.Close()` (`_lines.Count` doesn't care whether a line is voided),
  so the domain call succeeds and flips `Status` to `Closed` in memory —
  but `BuildFiscalLines` (`OrderEndpoints.cs`) omits every voided line, so
  `IFiscalProvider.IssueSimplifiedInvoiceAsync` receives zero lines and
  its own pre-existing `fiscal.no_lines` guard (there for unrelated
  reasons, predating ORD-10 entirely) rejects the whole close. Because
  `CloseOrderAsync` doesn't call `SaveChangesAsync` until *after* the
  fiscal document is actually issued, that in-memory `Close()` never
  reaches the database — the order stays genuinely `Open`, not
  closed-with-nothing-issued. This is correct, but it means "can Close()
  succeed" is not answerable by reading `Order.Close()` alone; a
  first-drafted doc comment on it guessed wrong (assumed a zero-value
  document would be issued) until the E2E suite caught the real
  `fiscal.no_lines` outcome. If a similar guard is ever added elsewhere,
  check what happens at the Fiscal boundary too, not just the Ordering
  aggregate's own state machine.

## 8. Environment

- Windows 10 Home. Shell is **PowerShell 5.1** — no `&&`, no ternary, no
  null-coalescing. Chain with `;` and `if ($?) { }`.
- .NET SDK 10.0.302 · Node 24.18.1 · Docker 29.6.2 · PostgreSQL 18.4 (container)
- The user commits locally and pushes to the remote themselves. **Make small,
  focused local commits as you work.**

```powershell
dotnet build Brasa.slnx                 # must be zero-warning
dotnet test  Brasa.slnx
dotnet test  tests/Brasa.Shared.Tests   # fast path
dotnet run   --project src/backend/Brasa.Api
docker compose -f infra/docker-compose.yml up -d
cd src/web/pos ; npm install ; npm run dev   # http://localhost:5173
cd src/web/admin ; npm install ; npm run dev -- --port 5174   # http://localhost:5174
cd src/web/e2e ; npm install ; npx playwright test   # starts API + pos + admin itself
```

## 9. Open blockers

| # | Blocker | Owner |
|---|---|---|
| 1 | **Portuguese legal entity not formed.** Cannot submit Modelo 24 without it. On the critical path to revenue, not to code — must start now | Founder |
| 2 | **VAT rules unconfirmed by an accountant.** The `TaxRule` model absorbs any answer, but rates must be verified before launch | Founder |
| 3 | **`Brasa` trademark and domains not cleared.** INPI (PT), EUIPO (classes 9/42), `.pt`/`.com` | Founder |

## 10. Keeping this file true

This brief is only worth reading if it is accurate. Update it in the **same
commit** as the change that dates it, specifically when:

- the current phase or next task changes (§3)
- a new invariant or trap is discovered (§2, §7)
- a new ADR lands (§6)
- a blocker opens or closes (§9)
- files or directories move (§4, and [repo-map.md](repo-map.md))

Bump **Last verified** at the top when you do.
