# Testing

The bar is **not uniform**. Effort goes where a defect costs the most: a mistake
in the fiscal engine is a customer's fine and our revoked certificate; a mistake
in a report layout is an annoyance.

## Tiers

| Tier | Area | Bar |
|---|---|---|
| 1 | Fiscal engine | Golden files, chain integrity, XSD validation, accountant review |
| 2 | Offline and sync | Chaos suite, idempotency replay, concurrent-terminal tests |
| 3 | Domain logic | Unit tests; exhaustive where the input space allows |
| 4 | Everything else | Reasonable coverage of the happy path and known edges |

## Running

```powershell
dotnet test Brasa.slnx                                   # everything
dotnet test tests/Brasa.Shared.Tests                     # fast, no Docker
```

Integration tests need a Docker daemon for Testcontainers. It is available on
the dev machine and on GitHub runners, so they run in both places.

## Tier 1 — Fiscal (highest bar)

- **Golden files.** Fixed inputs produce byte-identical signatures, ATCUDs and QR
  payloads. Fixtures live in `tests/Brasa.Fiscal.Portugal.Tests/Fixtures`
  and are copied to output. A diff here is either a bug or a change that requires
  notifying AT — never a casual update.
- **Chain integrity.** Issue 10,000 documents across interleaved series, then
  re-walk each chain asserting no gap and no break.
- **SAF-T validation** against AT's official XSD, in CI, so a schema regression
  fails the build.
- **AT test environment** round-trip for series registration and submission.
- **Accountant review** of real printed documents before submitting Modelo 24.
  Not automatable, and not optional.

## Tier 2 — Offline and sync

- **Chaos.** Kill the network mid-order, mid-payment, mid-print. Assert: no lost
  order, no duplicate document, **no gap in the series**.
- **Idempotency.** Replay every mutating request three times, assert a single
  effect.
- **Concurrency.** Two terminals editing the same table; assert the
  ownership-and-transfer protocol holds.

## Tier 3 — Domain

Exhaustive testing where the input space is small enough. `MoneyTests` checks
every cent value from 0 to €20.00 across six split counts — about 12,000
combinations — because "the bill split lost a cent" is a bug that must never
reach a restaurant.

## Integration tests

Run against **real PostgreSQL** via Testcontainers. No in-memory provider:
row-level security is a core part of what we are testing, and it does not exist
in-memory.

**Status:** built — `tests/Brasa.Api.IntegrationTests/TenantIsolationIntegrationTests.cs`
(QA-09/10). It asserts, against `Catalog`, that tenant A cannot read tenant
B's rows, that no tenant set returns nothing, and that the unprivileged role
cannot run DDL — queried with raw SQL as `brasa_app`, deliberately bypassing
the EF convenience filter, so a silently-disabled RLS policy (the ADR 0010
bug) can't hide behind it. Every module shares the identical mechanism
(`TenantAwareDbContext` + `RowLevelSecurity.EnableFor`), proven for every
entity by the companion reflection test (DAT-11,
`TenantIsolationReflectionTests`) — one thorough integration test against
one module's table, not yet one per module. Extend to Ordering/Floor
directly if a module-specific RLS bug is ever suspected.

## Load, before launch

Simulate 50 sites × 5 terminals at dinner-service order rates. Assert p95 API
latency under 200 ms, and that reporting queries never touch the transactional
path.

## Conventions

- `MethodName_does_the_expected_thing` — underscores are allowed in tests
  (`CA1707` is disabled under `tests/`).
- Shouldly for assertions.
- No shared mutable state between tests; each integration test gets a clean
  database.
- A test that documents a **legal** requirement should say so in a comment, with
  the rule it protects.
