# ADR 0009 — Incremental delivery, walking skeleton first

**Status:** Accepted · **Date:** 2026-08-08
**Supersedes the sequencing in** [ADR 0002's](0002-own-fiscal-engine.md) roadmap
section — not its fiscal decision, which stands.

## Context

The original plan sequenced work by layer over six months: foundations, then
catalog, then ordering, then the Site Agent, then fiscal. Each phase produced
infrastructure; nothing was demonstrable until Month 2.

That is a poor fit for a startup, for three reasons:

1. **No feedback.** A solo developer building for two months without a user
   touching it is two months of unvalidated assumptions — and the
   [differentiation thesis](../../product/differentiation.md) is explicitly
   unvalidated.
2. **No proof of integration.** Layered builds hide integration risk until the
   end, which is exactly where a POS is most likely to be wrong: offline
   behaviour, printing, concurrent terminals.
3. **Nothing to show.** Investors, pilot restaurants and accountants respond to
   a working demo, not an architecture diagram.

Revised targets: a demoable product in **week 1**, pilot-ready by **month 1**,
a working solution by **month 3**.

## Decision

**Deliver in vertical increments, each ending in a runnable demo.**

- **I0 is a walking skeleton** — the thinnest slice through every layer
  (browser → API → PostgreSQL → back), deployed, in week 1.
- Each subsequent increment **thickens** the skeleton rather than adding a new
  layer beneath it.
- **The demo script is the definition of done.** If it cannot be run start to
  finish, the increment is not finished.
- **Deployment is in I0**, not at the end. Weekly shipping is impossible without
  a pipeline.
- **Feature flags** (IDN-16) land in I3, so work merges to `main` continuously
  and is revealed per tenant when ready. Long-lived branches are the failure
  mode this avoids.

Sequencing lives in [../../product/roadmap.md](../../product/roadmap.md); task
status stays in [../../product/backlog.md](../../product/backlog.md).

### What is exempt from "defer it until needed"

Three things go in from the first line of code, because they are **conventions,
not features**, and conventions are only free if they start at the beginning:

| | Why it cannot wait |
|---|---|
| **Row-level security** (DAT-05) | A policy alongside the first table costs nothing. Retrofitting means auditing every query already written |
| **Idempotency keys** (API-05) | Retrofitting means auditing every mutation for double-effect |
| **`/api/v1` versioning** (API-01) | Moving an unversioned API later breaks every shipped client |

Deferring these would be borrowing against exactly the work that is hardest to
repay.

## Consequences

**Good**

- Feedback from real users in month 1 instead of month 6.
- Integration risk surfaces in week 1, when it is cheap.
- Something to show a pilot restaurant, an accountant, or an investor at any
  point.
- Motivation. A visible product beats a growing pile of infrastructure.

**Bad**

- **Some rework is guaranteed.** I0's single-screen POS will be rewritten by I2.
  That is accepted cost, not waste — the alternative is designing the screen
  before knowing how ordering behaves.
- **Weekly demos can crowd out testing.** Mitigated by landing the E2E harness
  alongside I0–I1 rather than after. A demo that only works when driven by hand
  is not a demo.
- **Fiscal sits late (I7).** Deliberate: it is the least changeable code, so it
  should be built against a stable domain. But it cannot slip past I7 without
  threatening the certification timeline.

## What this does not change

**Certification is binary, and no methodology compresses it.** There is no
half-certified state. Month 3's "working solution" is separated from paying
customers by AT's 30-day review, its conformity tests, and at least one
remediation cycle.

Piloting is unaffected: restaurants can run Brasa in **parallel-run mode** from
month 1 — real orders and service, fiscal documents still issued by their
existing certified system. Legal, no certificate required, and the source of the
feedback that matters.

## Revisit when

- Increments consistently overrun, indicating they are scoped by hope rather
  than capacity.
- Rework from thickening exceeds the value of the feedback it bought.
- A pilot restaurant commits, at which point their service calendar starts
  driving the increments instead.
