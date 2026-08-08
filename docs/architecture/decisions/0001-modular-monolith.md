# ADR 0001 — Modular monolith, not microservices

**Status:** Accepted · **Date:** 2026-08-08

## Context

The product must serve small independents on day one and multi-location chains
later, and it is being built by **one developer** with a six-month target.
Microservices are the reflexive answer to "must scale later".

## Decision

Ship **one deployable ASP.NET Core application**, internally divided into modules
with enforced boundaries:

- Each module owns its own EF Core schema.
- Modules **do not reference each other's projects** and never read each other's
  tables.
- Cross-module communication goes through integration events written to a
  transactional outbox, dispatched in-process.

## Consequences

**Good**

- One thing to deploy, debug, and monitor. For a solo developer, operating a
  distributed system would consume the entire six months.
- Transactions remain local, so "order closed" and "fiscal document issued" can
  be made atomic — which matters enormously in a system that must never leave a
  gap in a document series.
- The extraction seam already exists. Making a module a service later means
  swapping the dispatcher, not rewriting publishers and handlers.

**Bad**

- Boundaries are enforced by discipline and project references, not by the
  network. A determined shortcut can still cross them.
- The whole app scales as a unit until a module is extracted.

## Revisit when

- One module's load profile diverges sharply from the rest — most likely
  Reporting, or delivery-platform webhook ingestion.
- The team grows past roughly four developers and merge contention becomes real.
- A module needs an independent deployment cadence for compliance reasons.
