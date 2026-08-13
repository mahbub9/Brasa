# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Start here

**Read [docs/ai/README.md](docs/ai/README.md) before anything else.** It is a
single dense brief — invariants, current state, next task, repo map, and the
traps that look like bugs but are not — written so you can begin work without
scanning the repository. [docs/ai/repo-map.md](docs/ai/repo-map.md) has the
file-by-file inventory.

## What this is

Multi-tenant restaurant management SaaS for **Portugal**. Solo developer, .NET 10
backend, React PWA clients, targeting a first live restaurant in ~6 months.

The architecture is unusual and the reasons are documented; do not infer intent
from the code alone.

## Before you change anything

| Situation | Read first |
|---|---|
| Anything fiscal | [docs/fiscal/README.md](docs/fiscal/README.md) — legal constraints, not preferences |
| Money, totals, splitting | [docs/architecture/money.md](docs/architecture/money.md) |
| Adding or changing **any endpoint** | [docs/architecture/api-contract.md](docs/architecture/api-contract.md) — mobile-readiness rules |
| A new module or cross-module call | [docs/architecture/module-boundaries.md](docs/architecture/module-boundaries.md) |
| Tenant-scoped data | [docs/architecture/multi-tenancy.md](docs/architecture/multi-tenancy.md) |
| Wondering "why is it like this?" | [docs/architecture/decisions/](docs/architecture/decisions/) |
| Wondering "what do I build next?" | [docs/product/roadmap.md](docs/product/roadmap.md) — increments with demo scripts |
| Wondering "does this actually work yet?" | [docs/product/status.md](docs/product/status.md) |

## Hard rules

1. **Money is `Money`.** Never `double`, `float`, or bare `decimal`. Split bills
   with `Allocate`, never division.
2. **Never call `DateTime.UtcNow`.** Inject `IClock`.
3. **Never mutate an issued fiscal document.** Corrections are credit notes. A
   code path that can invisibly alter fiscal data is a *certification failure*,
   not a code smell.
4. **Modules never reference or query each other.** Use integration events.
5. **Expected failures return `Result`**, not exceptions.
6. **Do not weaken the build policy.** `TreatWarningsAsErrors` is on
   deliberately — it caught a transitive CVE on day one. Suppress in
   `.editorconfig` with a written reason, or fix the warning.
7. **No cookie auth, no web-only assumptions in the API.** Android and iOS ship
   soon after web and must need zero backend change. Every realtime message also
   has a REST equivalent; every mutation takes an idempotency key; error codes
   are a public contract whose meaning never changes once released.

## Commands

```powershell
dotnet build Brasa.slnx
dotnet test  Brasa.slnx
dotnet test  tests/Brasa.Shared.Tests      # fast, no Docker needed
dotnet run   --project src/backend/Brasa.Api
```

**Before committing any endpoint change, run
`infra/scripts/verify.ps1`.** It mirrors CI (`.github/workflows/ci.yml`)
locally — build, test, the OpenAPI-drift check (API-14), and the
vulnerable-package scan — so a drifted `docs/openapi/v1.json` or a fresh
transitive CVE is caught before a push, not after. Pass `-IncludeE2E` to
also run the full Playwright suite. This exists because CAT-02,
ORD-07/08/09 and CAT-17 each shipped its own endpoint without the
documented `docs/openapi/v1.json` regeneration step, and CI's own
`openapi-drift` job caught all three only after the push — see the trap
in `docs/ai/README.md`.

## Environment notes

- Windows 10 Home. Shell is **PowerShell 5.1** — no `&&`, no ternary, no
  null-coalescing. Chain with `;` and `if ($?) { }`.
- The solution file is **`Brasa.slnx`** (the .NET 10 XML format), not
  `.sln`.
- **Docker is installed and running** (`brasa-postgres`/`brasa-seq`
  containers, `infra/docker-compose.yml`) — integration tests
  (Testcontainers) run fine; `dotnet test Brasa.slnx` exercises them along
  with everything else. This was stale for a while (an earlier note here
  said Docker wasn't installed) — corrected once proven wrong by actually
  running `Brasa.Api.IntegrationTests` successfully. See
  [docs/development/getting-started.md](docs/development/getting-started.md).

## Documentation is part of the work

Update docs in the **same commit** as the code. Specifically:

- Finishing or starting a component → update
  [docs/product/status.md](docs/product/status.md).
- A non-obvious technical choice → add an ADR.
- Describing something not yet built → mark it `> **Status: stub.**` and use
  future tense.

The full contract is in
[docs/development/documentation.md](docs/development/documentation.md).

## Things that are easy to get wrong here

- **Series chaining is per-series, never global.** Two series advance
  independently.
- **The pre-bill given to a table is a *documento não fiscal***, not an invoice.
  Issuing it as an invoice would fiscalise every table that asks to see the bill.
- **VAT rates are not settled.** They are modelled as data with effective dates
  and need accountant confirmation. Do not hardcode them.
- **The Azores are an hour behind the mainland**, which affects daily close and
  SAF-T period boundaries.
- **Order lines copy the item name, price and VAT rate at the time of sale.** That
  is correctness, not denormalisation — a receipt must show what the item cost
  when it was sold.
