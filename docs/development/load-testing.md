# Load testing

> **Status:** 🚧 in progress (QA-13). Read-path and write-path scripts exist
> and have been run live against the real dev API with real, honest results
> below — but this row's own backlog title overclaims two things this
> doesn't (and can't yet) cover. See "What this doesn't prove" below before
> trusting these numbers for anything beyond this repository's current
> architecture.

## Why "sites" and "terminals" don't mean what the backlog title says yet

The original build plan's own verification bullet reads: *"Simulate 50
sites × 5 terminals at dinner-service order rates; assert p95 API latency
under 200ms and that reporting queries never touch the transactional
path."* Neither half of that is actually testable today:

- **"50 sites"** implies 50 distinct tenants generating independent load.
  Only one dev tenant exists (`DevTenantMiddleware`'s fixed dev tenant) —
  there is no multi-tenant seed data to simulate 50 sites *with*. Testing
  "50 sites" today would really just be testing "50× the concurrency
  against one tenant," which measures something different (single-tenant
  connection/lock contention, not cross-tenant isolation under load) and
  would be misleading to label the same way.
- **"5 terminals"** in the real architecture means 5 devices talking to a
  restaurant's own **Site Agent** over LAN, which syncs to the cloud —
  most real traffic never hits this API directly (see the Site Agent
  design in the root `CLAUDE.md`). The Site Agent doesn't exist yet (AGT
  epic, I4–I5), so *every* request currently goes straight to this cloud
  API — meaning today's realistic ceiling for "one restaurant's dinner
  service" is a handful of terminals hitting the cloud directly, not the
  cloud absorbing 250 terminals' worth of *synced* traffic the way it
  eventually will.
- **"Reporting queries never touch the transactional path"** — `Reporting`
  is an empty module (0 of 12 RPT tasks built). There are no reporting
  queries yet to prove don't interfere with anything.

So: this page measures **read and write latency against the current
single-tenant, direct-to-cloud architecture**, at a concurrency scaled to
what that architecture will actually see before the Site Agent exists —
useful, honest signal for today, not a substitute for the real "50 sites"
drill once there's a Site Agent and multi-tenant data to run it against.

## Scripts

Both live in `src/web/e2e/load/`, run via `npm run load` (both),
`npm run load:read`, or `npm run load:write` from `src/web/e2e`.

| Script | What it does |
|---|---|
| `read-load.mjs` | [autocannon](https://github.com/mcollina/autocannon) against `GET /menu` and `GET /floor` — the two endpoints every terminal polls constantly through a shift. No correlated state between requests, so a generic HTTP benchmarking tool fits directly. autocannon's own latency buckets skip from p90 straight to p97.5 — no exact p95 — so p97.5 is used as a deliberately conservative stand-in and labelled `p95 (p97.5)` in the output rather than silently claiming a percentile the tool doesn't actually report. |
| `write-load.mjs` | A hand-rolled harness: N concurrent "terminals," each claiming exactly one seeded table for the whole run and cycling **open → ring up an item → close (issues a real fiscal document) → clear** repeatedly for a fixed duration. Bounded by the 16-table seed pool — more terminals than tables would just produce table-conflict `409`s that look like failures but are really a fixture-size limit, not a performance one. |
| `run-all.mjs` | Runs both, exits non-zero if either failed. |

Environment variables (all optional, sensible defaults):

| Variable | Default | Meaning |
|---|---|---|
| `LOAD_DURATION` | `10` | Seconds each test runs |
| `LOAD_CONNECTIONS` | `50` | Concurrent connections for `read-load.mjs` |
| `LOAD_TERMINALS` | `10` | Concurrent terminals for `write-load.mjs`, capped at however many tables are actually `Free` |
| `BRASA_API_BASE_URL` | `http://localhost:5216` | Same convention as the Playwright E2E suite |

## The one real finding: Development-level logging is not a performance baseline

The first live run of `read-load.mjs` against the API running with its
normal `dotnet run` development settings (`Serilog:MinimumLevel:Default`
= `Debug`, per `appsettings.Development.json`) showed `GET /menu` at
**p50 = 2583ms** under just 10 concurrent connections — a genuinely
alarming number. A single sequential `curl` request against the same
running API completed in 16–50ms, ruling out the query or serialization
itself. Restarting the API with `Serilog__MinimumLevel__Default=Information`
(matching `appsettings.json`'s production default) and re-running the
identical test dropped `GET /menu` to **p50 = 67ms** — roughly a 40×
difference, from logging verbosity alone.

This isn't a bug in the application; `Debug` level logs every EF Core
command, connection open/close, and request-pipeline event to both the
console and Seq, which is exactly the right trade-off for interactive
solo-dev troubleshooting and exactly the wrong one to leave on while
measuring latency. **Every number below was captured with
`Serilog__MinimumLevel__Default=Information`** — set that environment
variable (or use a `Production`-shaped config) before trusting a load
test run against this API for anything beyond "does the code path work,"
or the numbers will mostly measure logging I/O, not the application.

## Results (live, `Information`-level logging)

Captured on this project's primary dev machine (see
`getting-started.md`'s "Verified environment" table) against the seeded
dev database — not a clean/idle system, but not artificially quiet either.

**Read path**, 20 concurrent connections, 10s (a generous "several
terminals actively polling one site" proxy — see the scoping note above):

```
GET /menu   n=2134  p50=85ms   p95=199ms  p99=230ms  max=338ms
GET /floor  n=3644  p50=50ms   p95=112ms  p99=134ms  max=264ms
PASS -- both endpoints stayed under the 200ms p95 target.
```

At 50 connections (deliberately past today's realistic ceiling, to see
where it degrades): `GET /menu` p95 rises to ~830ms. Not a red flag by
itself — 50 simultaneous direct-to-cloud requests from a *single site* is
already well past what 5 terminals produce even at a busy dinner service —
but it is the number to revisit once concurrency genuinely needs to scale
past a handful of terminals per tenant (i.e., once the Site Agent exists
and most traffic no longer hits this API directly per-request).

**Write path**, 10 concurrent terminals, 15s (bounded by the 16-table
pool):

```
551 full open -> add line -> close -> clear cycles completed, 0 failures

open      n=551  p50=62ms  p95=107ms  p99=132ms
addLine   n=551  p50=70ms  p95=114ms  p99=143ms
close     n=551  p50=81ms  p95=132ms  p99=168ms
clear     n=551  p50=38ms  p95=71ms   p99=94ms
```

All four operations — including `close`, which issues a real mock fiscal
document (`IFiscalProvider`) — stayed comfortably under the 200ms target
with zero failed requests across 2,204 total mutating calls (each closed
order also implicitly exercises the idempotency middleware, since every
mutation carries its own `Idempotency-Key`).

## What this doesn't prove

- **Not 50 real sites.** See above — no multi-tenant seed data exists to
  drive genuinely independent tenant load, and RLS/tenant-isolation
  behaviour under concurrent cross-tenant load specifically is QA-09/10's
  job (already covered, see `status.md`), not this page's.
- **Not the eventual real traffic shape.** Once the Site Agent (AGT) and
  offline sync (SYN) exist, most POS traffic never reaches this API
  per-request at all — it syncs in batches. Today's "every request hits
  the cloud directly" numbers will not be the right numbers to compare
  against once that's built.
- **Not reporting-vs-transactional isolation.** `Reporting` doesn't exist
  yet (RPT epic, I8).
- **Not tested against Testcontainers or a clean database.** This runs
  against the same persistent dev database every other manual/E2E
  verification in this repository uses — closer to a real, lived-in
  system than a pristine benchmark environment, deliberately.
