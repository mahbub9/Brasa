# AI session brief — start here

> **You are picking up development on this repository. Read this file
> completely before reading any source.** It is written so you can be productive
> without scanning the tree. It is maintained deliberately; if it is wrong, fix
> it in the same commit as whatever proved it wrong.

**Last verified:** 2026-08-08 · **Phase:** Month 0 complete → Month 1 starting

---

## 1. What this is, in sixty seconds

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

## 3. Where things stand

**Authoritative inventory: [../product/status.md](../product/status.md).** Read
it — it marks every empty project explicitly, because a scaffold makes empty
things look finished.

Condensed:

- ✅ **Built and tested:** solution + build policy, `Money` (17 tests, exhaustive
  allocation), `Result`/`Error`, `ITenantContext`, `IClock` + Portuguese regions,
  `Entity`/UUIDv7, outbox *contracts*, API bootstrap (`/health`, `/api/v1/ping`),
  Docker Compose (PostgreSQL 18 + Seq), full docs tree, CI.
- 📁 **Empty projects (structure only, zero logic):** all six modules,
  `Fiscal.Portugal`, `Fiscal.Mock`.
- 🚧 **Stub:** `SiteAgent` starts and stops; nothing else.
- ⬜ **Not started:** EF Core + RLS, all web clients, everything fiscal.

**Next task:** wire EF Core + PostgreSQL with `tenant_id`, global query filters,
and the row-level security migration. Then the Identity module.

## 4. Repo map

Detailed file-by-file inventory: [repo-map.md](repo-map.md).

```
src/backend/
  RestaurantPos.Api               Cloud API. Minimal APIs under /api/v1
  RestaurantPos.Shared            Shared kernel — depends on no module
  RestaurantPos.Modules.Identity  Users, roles, staff PINs, terminal pairing
  RestaurantPos.Modules.Catalog   Menu, modifiers, price lists, tax rules
  RestaurantPos.Modules.Ordering  Orders, tables, courses, splits, transfers
  RestaurantPos.Modules.Fiscal    IFiscalProvider, document lifecycle, audit
  RestaurantPos.Modules.Payments  Tenders, cash sessions, tips
  RestaurantPos.Modules.Reporting Read models, X/Z reports, VAT summaries
  RestaurantPos.Fiscal.Portugal   ATCUD, RSA chain, QR, SAF-T, AT webservices
  RestaurantPos.Fiscal.Mock       Deterministic fake for dev and tests
src/agent/
  RestaurantPos.SiteAgent         In-restaurant worker: signing, printing, LAN hub
tests/                            Unit, fiscal golden-file, integration
docs/                             This documentation tree
infra/                            docker-compose (PostgreSQL 18, Seq)
```

## 5. Task → where to look

| Doing this | Read |
|---|---|
| Anything fiscal | [../fiscal/README.md](../fiscal/README.md) — legal constraints, not preferences |
| Money, totals, VAT, splitting | [../architecture/money.md](../architecture/money.md) |
| Adding a module, or a cross-module call | [../architecture/module-boundaries.md](../architecture/module-boundaries.md) |
| Tenant-scoped data, RLS | [../architecture/multi-tenancy.md](../architecture/multi-tenancy.md) |
| Offline, printing, the agent | [../architecture/site-agent.md](../architecture/site-agent.md) |
| "Why is it built like this?" | [../architecture/decisions/](../architecture/decisions/) |
| Code style, analyzer policy | [../architecture/conventions.md](../architecture/conventions.md) |
| Testing expectations | [../development/testing.md](../development/testing.md) |
| A Portuguese term you don't know | [../glossary.md](../glossary.md) |
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

## 7. Traps — things that look wrong but are intentional

- **Order lines copy the item name, price and VAT rate.** That is correctness,
  not denormalisation. A receipt must show what the item cost *when it was sold*.
- **The pre-bill given to a table is a *documento não fiscal*.** Issuing it as an
  invoice would fiscalise every table that merely asks to see the bill.
- **VAT rates are data with effective dates, not constants.** They are unconfirmed
  by an accountant and politically contested. Never hardcode them.
- **The Azores are an hour behind the mainland.** This moves the daily close and
  SAF-T period boundaries. `PortugueseRegion` exists for this.
- **`Money.Format(culture)` is not called `ToString`.** Deliberate — it forces
  callers to name the culture, and keeps `ToString()` unambiguously invariant.
- **`Fiscal.Mock` must never run in Production.** It produces structurally valid
  but fiscally meaningless documents.
- **The solution file is `RestaurantPos.slnx`**, the .NET 10 XML format — not `.sln`.
- **PostgreSQL 18 mounts `/var/lib/postgresql`**, not `.../data`. Mounting
  `.../data` makes the container refuse to start.

## 8. Environment

- Windows 10 Home. Shell is **PowerShell 5.1** — no `&&`, no ternary, no
  null-coalescing. Chain with `;` and `if ($?) { }`.
- .NET SDK 10.0.302 · Node 24.18.1 · Docker 29.6.2 · PostgreSQL 18.4 (container)
- The user commits locally and pushes to the remote themselves. **Make small,
  focused local commits as you work.**

```powershell
dotnet build RestaurantPos.slnx                 # must be zero-warning
dotnet test  RestaurantPos.slnx
dotnet test  tests/RestaurantPos.Shared.Tests   # fast path
dotnet run   --project src/backend/RestaurantPos.Api
docker compose -f infra/docker-compose.yml up -d
```

## 9. Open blockers

| # | Blocker | Owner |
|---|---|---|
| 1 | **Portuguese legal entity not formed.** Cannot submit Modelo 24 without it. On the critical path to revenue, not to code — must start now | Founder |
| 2 | **VAT rules unconfirmed by an accountant.** The `TaxRule` model absorbs any answer, but rates must be verified before launch | Founder |

## 10. Keeping this file true

This brief is only worth reading if it is accurate. Update it in the **same
commit** as the change that dates it, specifically when:

- the current phase or next task changes (§3)
- a new invariant or trap is discovered (§2, §7)
- a new ADR lands (§6)
- a blocker opens or closes (§9)
- files or directories move (§4, and [repo-map.md](repo-map.md))

Bump **Last verified** at the top when you do.
