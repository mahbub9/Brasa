# AI session brief — start here

> **You are picking up development on this repository. Read this file
> completely before reading any source.** It is written so you can be productive
> without scanning the tree. It is maintained deliberately; if it is wrong, fix
> it in the same commit as whatever proved it wrong.

**Last verified:** 2026-08-09 · **Phase:** I0 complete except deployment (OPS-11); I1's floor plan and menu modifiers proven live end-to-end; I2's pre-bill preview (ORD-18/19), order history/search (ORD-22), kitchen notes (ORD-06), table transfer (ORD-12), line transfer (ORD-13) and order merge (ORD-14) pulled forward and done

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
  API-04 — see [error-codes.md](../architecture/error-codes.md)), tenancy +
  **real RLS** (DAT-01…06),
  `Catalog` (categories/items, seeded, soft delete — CAT-18, modifier groups
  — CAT-03/04), `Ordering` (open against a real table/add-line-with-modifiers
  — ORD-05/per-line kitchen notes — ORD-06/transfer to a different table —
  ORD-12/transfer a single line to a different order — ORD-13/merge two
  orders — ORD-14, new `OrderStatus.Merged`, no migration needed/split/
  pre-bill preview — ORD-18/19, provably non-fiscal, see §7/close/history-
  search — ORD-22), `Floor` (rooms, tables, full `Free ⇄ Occupied ⇄ Dirty ⇄ Free`
  lifecycle, `xmin` optimistic concurrency — FLR-01/02/04), `Fiscal` contract
  + `Fiscal.Mock`, API layer (versioning, ProblemDetails, idempotency, CORS,
  the full order flow composing all four modules), the `pos` web shell
  (React 19 + Vite + TS, table-picker → order incl. a modifier picker →
  receipt, WEB-01/05, pt-PT default / en toggle behind a mobile-portable
  cookie seam — WEB-13, ADR 0011), a Playwright E2E harness driving the real
  UI (`src/web/e2e`, QA-01/03/05/14 incl. axe-core accessibility scans, 27
  tests green on a clean run — the seeded floor plan was doubled to 16
  tables after back-to-back full runs started exhausting the original 8, a
  QA-02 scaling limitation, not a product bug; see
  [e2e-testing.md](../development/e2e-testing.md)),
  `Brasa.Api.IntegrationTests` (DAT-11,
  QA-09/10 — real Testcontainers Postgres proving tenant isolation by
  automated test, not just manual psql anymore), a liveness/readiness split
  (`/health`, `/health/ready` — OPS-09, live-verified against a stopped and
  restarted PostgreSQL container), Docker Compose
  (PostgreSQL 18 + Seq), full docs tree, CI (including an `e2e` job —
  written, not yet run in CI).
- 📁 **Empty projects (structure only, zero logic):** `Modules.Identity`,
  `Modules.Payments`, `Modules.Reporting`, `Fiscal.Portugal`.
- 🚧 **Stub:** `SiteAgent` starts and stops; nothing else.
- ⬜ **Not started:** `kds`/`admin`/`order` web clients, deployment.

**Delivery is incremental** — vertical slices, each ending in a runnable demo.
**[../product/roadmap.md](../product/roadmap.md) says what to build next**;
[../product/backlog.md](../product/backlog.md) holds the 291 tasks and their
status. Reference IDs in commits: `feat(identity): terminal pairing (IDN-07)`,
and update the status in the same commit.

**Current increment: I0 is done except deployment (OPS-11).** I1 ("Menu and
floor," see roadmap) is well underway — floor plan (FLR-01/02/04, WEB-05) and
modifiers (CAT-03/04) are both done and proven; price lists (CAT-05) and the
`admin` back-office shell are not.

Backend/I0 tasks — **done**: DAT-01/03/04/**05**/06/**11**/10 · API-01/03/05 ·
CAT-01/02/03/04/07/18 ·
ORD-01/02/03/04/**05**/**06**/**12**/**13**/**14**/15/**18**/**19**/**22** ·
FIS-01/02/03 · WEB-01/05/13 · QA-01/03/05/**09**/**10**/**14** · FLR-01/02/04 ·
API-**04** · OPS-**09**.

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
- **`pos` never dims text with CSS `opacity` for visual hierarchy.** It looks
  fine to a sighted reviewer and quietly fails WCAG contrast anyway —
  `opacity` blends the color toward whatever's behind it, so the *effective*
  contrast is lower than the raw foreground color suggests. `accessibility.spec.ts`
  (QA-14) caught five of these on its first run. Pick a genuinely-compliant
  color instead; see [status.md](../product/status.md#accessibility-first-scan-five-real-fixes).
- **`Money.Format(culture)` is not called `ToString`.** Deliberate — it forces
  callers to name the culture, and keeps `ToString()` unambiguously invariant.
- **`Fiscal.Mock` must never run in Production.** It produces structurally valid
  but fiscally meaningless documents.
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
cd src/web/e2e ; npm install ; npx playwright test   # starts API + pos itself
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
