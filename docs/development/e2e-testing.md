# End-to-end testing

> **Status: I0/I1 harness built, plus a first slice of I2.** `src/web/e2e` —
> Playwright + TypeScript, against the real `pos` UI and the real API
> (QA-01/02/03/05/09/10/14 — see [../product/backlog.md](../product/backlog.md)).
> QA-04 (clock control) and QA-06 (offline chaos) are **not** built yet — see
> §"What's actually built" below for why, honestly, rather than pretending
> they're covered.

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

## What's actually built

`src/web/e2e` (Playwright + TypeScript, chromium only for now):

- **`playwright.config.ts`** — `webServer` starts the API
  (`dotnet run --no-build`), the `pos` dev server (port 5173) and the
  `admin` dev server (port 5174, WEB-09), so `npx playwright test` works
  standalone. **Docker (PostgreSQL) is a prerequisite it does not start** —
  same as `dotnet run` locally. If Docker isn't up, the API's `webServer`
  entry fails fast with a clear error instead of hanging. `admin`'s origin
  needs its own entry in the API's `Cors:AllowedOrigins`
  (`appsettings.Development.json`) — a second web client is what actually
  exercises that config path for the first time.
- **`tests/walking-skeleton.spec.ts`** (QA-05) — drives the real rendered
  `pos` UI in a real browser: pick a free table, ring up 2× Frango na Brasa
  (through the modifier picker) + 2× Imperial, preview a 3-way split, close,
  check the receipt, clear the table. Deliberately does **not** use the API
  builders below — it exists to prove the UI itself works, which nothing
  before it had actually done (see
  [status.md](../product/status.md#i0-demo-verified-live-not-just-unit-tested)
  for the gap this closed).
- **`tests/modifiers.spec.ts`** (CAT-03/04) — the modifier picker itself:
  a required group blocks "Add" until satisfied, Cancel adds nothing, and
  selected price deltas total correctly on the rung-up line.
- **`tests/accessibility.spec.ts`** (QA-14) — `@axe-core/playwright` against
  the table picker, ordering screen, modifier picker modal, and receipt,
  scoped to WCAG 2.0/2.1 A + AA (axe's defaults minus the opinionated
  "best-practice" tag). Found and fixed five real `color-contrast` failures
  on its first run — see
  [status.md](../product/status.md#accessibility-first-scan-five-real-fixes).
  Covers `pos` only; there's no guest-facing UI yet to check.
- **`tests/support/api.ts`** (QA-03) — typed builders (`openOrder`,
  `addLine`, `getMenu`, `findMenuItem`, `defaultRequiredModifierIds`) for
  setting up order state via the API directly, for specs that aren't
  exercising the ordering UI itself. Menu items and tables are looked up
  **by name/state**, never by id — ids are UUIDv7, generated at seed time,
  and not stable across a fresh database.
  `openOrderOnAnyFreeTable`/`closeOrderAndClearTable` retry on a 409 and
  return tables to the free pool respectively — table state is genuinely
  contended now (see the concurrency note below), not just a formality.
- **`tests/support/ui.ts`** — `openAnyFreeTable`, the UI-driven counterpart:
  clicks a free table, and on a 409 (shown as an error banner, not a thrown
  exception, since the UI handles it) dismisses it and retries against
  whatever the refreshed floor state now shows as free.
- **`tests/split-preview.spec.ts`** — uses the API builders to sweep
  `Money.Allocate` across several part counts (1, 2, 3, 5, 7) purely at the
  API level (Playwright's `request` fixture, no browser), asserting shares
  always sum back to the total to the cent and never differ from each other
  by more than one cent. This is intentionally an API-level test, not a UI
  one — it's cheap enough to sweep cases the UI spec doesn't bother
  enumerating.
- **`tests/pre-bill.spec.ts`** (ORD-18/19) — the wire shape has no fiscal
  fields at all (no document number, ATCUD or QR), VAT-band reconciliation,
  a reprint matching byte-for-byte, 400/409 guards, and a WCAG scan on the
  dialog.
- **`tests/order-history.spec.ts`** (ORD-22) — `GET /orders` filtering by
  status/table shows a just-opened order and drops it the instant it closes,
  correct totals and line counts, invalid-filter 400s.
- **`tests/line-notes.spec.ts`** (ORD-06) — set/overwrite/clear a line's
  kitchen note via the API, unknown-line/too-long/closed-order guards, and
  the inline editor in `pos` (re-opening starts from the saved value, not a
  stale draft).
- **`tests/transfer-table.spec.ts`** (ORD-12) — moving an order to a
  different table frees the old one and occupies the new one in the same
  response cycle, the order's lines survive untouched, and the guards
  (occupied target, unknown table, closed order) all fire. The UI case uses
  `transferToAnyFreeTable` (`support/ui.ts`) — a second, independent
  retry-on-409 loop layered on top of `openAnyFreeTable`'s — and needs a
  longer test timeout (`test.setTimeout(120_000)`) because their worst
  cases can add up under real contention for the 8-table pool.
- **CI** — a `.github/workflows/ci.yml` job (`e2e`) starts Postgres via
  `infra/docker-compose.yml`, builds the API in Debug (what `--no-build`
  picks up), installs both npm projects and Chromium, and runs the suite.
  **This job has not actually been run in CI** — no push has triggered it
  yet. It mirrors what was verified locally (warm-server and cold-start, both
  green) but treat it as reviewed, not proven, until it has gone green in an
  actual GitHub Actions run.

Verified locally from a warm state, from a cold start (both dev processes
killed first, so `webServer` launching them from nothing is actually
exercised), and across many consecutive full runs under Playwright's real
2-worker parallelism. That last form of repetition is what actually matters
here: a single green run never exercised two tests genuinely racing for the
same table, and that race is exactly what surfaced a real concurrency bug in
`Table.Occupy()` — see
[status.md](../product/status.md#a-real-concurrency-bug-found-by-running-the-suite-enough-times).
23/23 passing on a clean run.

**A growing-pains flake, not a product bug.** Running the full suite
back-to-back with no pause in between (heavier than any real CI run, which
fires once per push) occasionally exhausts the 8-table pool — a test's
`openAnyFreeTable`/`openOrderOnAnyFreeTable` retries (5 attempts) can all
lose the race when several other specs are simultaneously mid-flight against
the same never-reset dev database. This is QA-02's already-documented
limitation showing up in practice as the suite has grown past twenty tests,
not a defect in any one spec. If you hit "No free table available", check
`GET /orders?status=Open` for a leftover order, close it, `POST
/tables/{id}/clear` any `Dirty` table, and re-run — or just wait for a
disposable-per-run database (see "What's next" below).

### What's deliberately not built yet

- **QA-04 (clock control).** No test-only `IClock` override exists in the
  API. Nothing the E2E suite currently exercises is time-sensitive enough to
  need it — no daily close, no business-day rollover, no SAF-T period
  boundary is wired up yet. Building clock control now, before anything
  depends on it, would be speculative. Revisit when AGT (Site Agent) or FIS
  (SAF-T) work lands.
- **QA-06 (offline chaos).** The `pos` shell has **no offline capability at
  all** yet — no Dexie, no service worker, no outbox (that's `WEB-04`,
  depending on the `SYN` epic). An E2E test that kills the network right now
  would just prove the obvious: the app breaks, because it was never built to
  survive it. Writing that test today would either be a tautology or, worse,
  get quietly skipped and rot. Build it when `WEB-04`/`SYN` ship, not before.

## The decision made (QA-01)

| Option | For | Against |
|---|---|---|
| **Playwright** (chosen) | First-class offline simulation (`context.setOffline`), clock control (`page.clock`), multiple isolated browser contexts for multi-terminal tests, trace viewer for debugging failures, cross-browser | Another toolchain, though Node is already present for the docs site and web clients |
| Cypress | Pleasant authoring experience, large community | Weak multi-origin/multi-context support, which makes multi-terminal tests awkward; offline simulation is limited |
| Selenium | Mature, any language | Dated API, slow, no built-in offline or clock control |
| Puppeteer | Lightweight | Chromium only; we need to prove the PWA works on the browsers real tablets ship with |

**Decision: Playwright with TypeScript**, living in `src/web/e2e` alongside
the clients it drives. It currently has its own small hand-written API
helpers (`tests/support/api.ts`) rather than the generated SDK, because
`web/sdk` (`WEB-03`) doesn't exist yet — only `pos` does. Switch
`support/api.ts` to the generated SDK once `WEB-03` lands, the same way
`pos`'s own `src/api/` will.

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

## What's next

1. ~~**QA-02** harness: API + PostgreSQL + seeded tenant~~ — done, though the
   database is the persistent dev one (`docker compose up -d`), not spun up
   fresh per run. A disposable-per-run database (Testcontainers-driven, or a
   `docker compose` invocation from `globalSetup`) is a reasonable follow-up
   once tests start wanting to run in parallel against independent data —
   **now observably worth doing**: at 23 tests sharing 8 seeded tables,
   back-to-back full runs have started occasionally exhausting the pool (see
   the note above). A cheaper interim mitigation — more seeded tables in
   `DevFloorSeeder` — buys headroom without the bigger lift of per-run
   isolation.
2. ~~**QA-05** happy path~~ — done (`walking-skeleton.spec.ts`).
3. ~~Wire into CI~~ — done (`.github/workflows/ci.yml`, job `e2e`), **not yet
   verified by an actual CI run**.
4. **QA-06** the offline suite above — blocked on `WEB-04`/`SYN` (no offline
   capability exists to test yet).
5. **QA-08** multi-terminal concurrency — blocked on `ORD-21` (ownership /
   conflict protocol doesn't exist yet either). A narrower slice of this —
   two requests racing to occupy the *same table* — is incidentally covered
   now: `Table`'s `xmin` concurrency token and `openOrderOnAnyFreeTable`/
   `openAnyFreeTable`'s retry-on-409 were built specifically because that
   race showed up under this suite's own parallelism. Full multi-terminal
   *order* ownership is still unbuilt.
6. **QA-07** split-bill flows — partially covered (`split-preview.spec.ts`
   covers even splits); by-item and by-cover splits don't exist yet
   (`ORD-16`/`ORD-17`).
7. ~~**DAT-11** / **QA-09/10** tenant isolation~~ — done, but lives in
   `tests/Brasa.Api.IntegrationTests` (.NET, Testcontainers), not here — see
   [testing.md](testing.md#integration-tests). This harness is specifically
   the browser/UI layer.

## Not in scope here

Unit and integration testing conventions live in [testing.md](testing.md).
This page is only about full-stack, browser-driven tests.
