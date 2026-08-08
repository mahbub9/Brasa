# End-to-end testing

> **Status: stub.** No E2E tests exist. This is the next session's work —
> epic **QA** in [../product/backlog.md](../product/backlog.md). The page
> records the decision to be made and the problems it has to solve, so the
> session starts from analysis rather than a blank page.

## Why this needs a plan

A restaurant POS fails in ways a normal web app does not, and the interesting
bugs live precisely where unit tests cannot reach:

1. **The network dies mid-service.** Not "the request failed" — the network dies
   between sending an order to the kitchen and taking payment for it.
2. **Two terminals touch the same table.** A waiter and a manager, at the same
   moment, on different devices.
3. **Time matters.** Business days roll at 04:00, not midnight. The Azores are an
   hour behind. A test that uses the real clock is a test that fails at 23:00.
4. **Money must reconcile to the cent.** The Z report, the fiscal documents and
   the payments must agree, or we have a compliance failure rather than a bug.
5. **The Site Agent is a separate process.** A true end-to-end run involves the
   API, PostgreSQL, the agent and the browser.

Unit tests cover `Money`. Integration tests will cover RLS. **Only E2E can prove
a service actually survives a Saturday night.**

## The decision to make (QA-01)

| Option | For | Against |
|---|---|---|
| **Playwright** (recommended) | First-class offline simulation (`context.setOffline`), clock control (`page.clock`), multiple isolated browser contexts for multi-terminal tests, trace viewer for debugging failures, cross-browser | Another toolchain, though Node is already present for the docs site and web clients |
| Cypress | Pleasant authoring experience, large community | Weak multi-origin/multi-context support, which makes multi-terminal tests awkward; offline simulation is limited |
| Selenium | Mature, any language | Dated API, slow, no built-in offline or clock control |
| Puppeteer | Lightweight | Chromium only; we need to prove the PWA works on the browsers real tablets ship with |

**Recommendation: Playwright with TypeScript**, living in the web workspace
alongside the clients it drives, sharing the generated SDK (API-15) for test
setup and assertions.

Playwright's three capabilities map exactly onto the three hardest problems
above: `setOffline` for outages, `page.clock` for business-day boundaries, and
independent browser contexts for concurrent terminals. That alignment is the
argument — not popularity.

> Playwright for .NET also exists. TypeScript is preferred because the tests
> drive React clients and can reuse the generated SDK, so test setup speaks the
> same types as the app.

## Problems to solve when building it

### Deterministic fiscal output
Signing must be reproducible. E2E runs against `Fiscal.Mock` (FIS-02), which
produces structurally valid, deterministic documents. Never against real keys.

### Test data
Seeded tenant, site, terminal, menu and staff, built from typed builders
(QA-03), not SQL fixtures. Every run starts from a known state on a disposable
database.

### Clock control
The API needs a test-only `IClock` that E2E can set, so business-day rollover
and Azores offsets are testable at any wall-clock time (QA-04). It must be
impossible to enable outside a test environment.

### Site Agent in the loop
Decide whether E2E runs against a real agent process or a stub. Real is more
honest and slower. A likely split: most flows against a stub, a small suite
against the real agent.

### The offline suite (QA-06)
The scenario that matters most:

```
1. Seat a table, take an order, fire to kitchen        [online]
2. Kill the network                                     [offline]
3. Add a round, split the bill, take payment            [offline]
4. Issue a fatura simplificada — valid ATCUD, QR, chain  [offline]
5. Restore the network                                  [online]
6. Assert: everything synced once, no duplicate document,
   no gap in the series, Z report reconciles to the cent
```

If that passes reliably, the core product promise is proven.

## What to build first

Order matters — QA-05 is the smoke test everything else builds on.

1. **QA-02** harness: API + PostgreSQL + seeded tenant, disposable per run
2. **QA-05** happy path: seat → order → fire → pay → close
3. **QA-06** the offline suite above
4. **QA-08** multi-terminal concurrency
5. **QA-07** split-bill flows
6. Wire into CI, with traces uploaded on failure

## Not in scope here

Unit and integration testing conventions live in [testing.md](testing.md).
This page is only about full-stack, browser-driven tests.
