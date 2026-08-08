# AI session brief — start here

> **You are picking up development on this repository. Read this file
> completely before reading any source.** It is written so you can be productive
> without scanning the tree. It is maintained deliberately; if it is wrong, fix
> it in the same commit as whatever proved it wrong.

**Last verified:** 2026-08-09 · **Phase:** I0 backend and POS web shell proven live end-to-end; deployment and E2E harness remain

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
| 11 | Error codes are a **public contract** — once released, the meaning never changes | Mobile clients branch on them and cannot be patched quickly |

## 3. Where things stand

**Authoritative inventory: [../product/status.md](../product/status.md).** Read
it — it marks every empty project explicitly, because a scaffold makes empty
things look finished.

Condensed:

- ✅ **Built, tested, and proven live** (not just unit-tested — see §3a):
  `Money` (17 tests), `Result`/`Error`, tenancy + **real RLS** (DAT-01…06),
  `Catalog` (categories/items, seeded), `Ordering` (open/add-line/split/close),
  `Fiscal` contract + `Fiscal.Mock`, API layer (versioning, ProblemDetails,
  idempotency, CORS, the full order flow), the `pos` web shell (React 19 +
  Vite + TS, one screen, WEB-01), Docker Compose (PostgreSQL 18 + Seq), full
  docs tree, CI.
- 📁 **Empty projects (structure only, zero logic):** `Modules.Identity`,
  `Modules.Payments`, `Modules.Reporting`, `Fiscal.Portugal`.
- 🚧 **Stub:** `SiteAgent` starts and stops; nothing else.
- ⬜ **Not started:** `kds`/`admin`/`order` web clients, the E2E harness, deployment.

**Delivery is incremental** — vertical slices, each ending in a runnable demo.
**[../product/roadmap.md](../product/roadmap.md) says what to build next**;
[../product/backlog.md](../product/backlog.md) holds the 290 tasks and their
status. Reference IDs in commits: `feat(identity): terminal pairing (IDN-07)`,
and update the status in the same commit.

**Current increment: I0 — walking skeleton, week 1.** Backend and the `pos`
web shell are done and proven; remaining: deployment (OPS-11) and the
Playwright E2E harness (QA-01…06 — see
[../development/e2e-testing.md](../development/e2e-testing.md)).

Backend I0 tasks — **done**: DAT-01/03/04/**05**/06/10 · API-01/03/05 ·
CAT-01/02/07 · ORD-01/02/03/04/15 · FIS-01/02/03 · WEB-01.

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

**The lesson, not just the fix:** a clean build and green unit tests proved
nothing about whether tenant isolation actually worked. If you build something
that depends on database-level behaviour (RLS, triggers, constraints), run it
against the real database and try to break it before calling it done.

The `pos` web shell was then verified the same way — `curl` reproducing the
exact browser request sequence (CORS preflight, `Origin` header,
`Idempotency-Key`) against the running API, confirming every DTO shape matches
the shell's TypeScript types field-for-field. It was **not** driven inside an
actual rendered browser — no browser-automation tool was available in that
session. If you have one, open `localhost:5173` and actually click through it
before trusting the UI beyond "it builds and the wire contract matches."

## 4. Repo map

Detailed file-by-file inventory: [repo-map.md](repo-map.md).

```
src/backend/
  Brasa.Api               Cloud API. Minimal APIs under /api/v1
  Brasa.Shared            Shared kernel — depends on no module
  Brasa.Modules.Identity  Users, roles, staff PINs, terminal pairing
  Brasa.Modules.Catalog   Menu, modifiers, price lists, tax rules
  Brasa.Modules.Ordering  Orders, tables, courses, splits, transfers
  Brasa.Modules.Fiscal    IFiscalProvider, document lifecycle, audit
  Brasa.Modules.Payments  Tenders, cash sessions, tips
  Brasa.Modules.Reporting Read models, X/Z reports, VAT summaries
  Brasa.Fiscal.Portugal   ATCUD, RSA chain, QR, SAF-T, AT webservices
  Brasa.Fiscal.Mock       Deterministic fake for dev and tests
src/agent/
  Brasa.SiteAgent         In-restaurant worker: signing, printing, LAN hub
src/web/
  pos                     POS PWA (React + TS + Vite) — I0 shell, WEB-01
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

## 7. Traps — things that look wrong but are intentional

- **Order lines copy the item name, price and VAT rate.** That is correctness,
  not denormalisation. A receipt must show what the item cost *when it was sold*.
- **The pre-bill given to a table is a *documento não fiscal*.** Issuing it as an
  invoice would fiscalise every table that merely asks to see the bill.
- **VAT rates are data with effective dates, not constants.** They are unconfirmed
  by an accountant and politically contested. Never hardcode them.
- **Menu prices are VAT-inclusive (gross), not net.** `MenuItem.Price` and
  `OrderLine.UnitPrice` are the final amount the guest pays. A fiscal document
  *derives* net and VAT from the gross price — computing it the other way
  round makes the running order total disagree with what the fiscal document
  charges. See `docs/fiscal/README.md`.
- **The Azores are an hour behind the mainland.** This moves the daily close and
  SAF-T period boundaries. `PortugueseRegion` exists for this.
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
