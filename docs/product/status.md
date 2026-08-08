# Build status

> **Purpose:** a project scaffold makes empty things look finished. This page is
> the honest inventory of **which code exists**. Update it in the same commit
> that changes reality.
>
> For **what to build next and task-level progress**, see
> [backlog.md](backlog.md) — 278 tasks with stable IDs. This page is
> component-level; the backlog is task-level.

**Last updated:** 2026-08-09 · **Roadmap phase:** I0 — walking skeleton, backend, POS web shell (with pt/en i18n) and a first E2E harness proven end-to-end; only deployment (OPS-11) remains

## Legend

| | Meaning |
|---|---|
| ✅ | Built, tested, works |
| 🚧 | Partially built |
| 📁 | Empty project — exists so the structure is visible, contains no logic |
| ⬜ | Not started |

## Backend

| Component | State | Notes |
|---|---|---|
| Solution + build pipeline | ✅ | .NET 10, central package management, zero-warning build |
| `Brasa.Shared` — `Money` | ✅ | Integer cents, allocation-based splitting, 17 tests passing |
| `Brasa.Shared` — `Result`/`Error` | ✅ | Expected failures as values |
| `Brasa.Shared` — tenancy | ✅ | `ITenantContext`, resolve-once-per-scope `TenantContext` |
| `Brasa.Shared` — time | ✅ | `IClock`, `PortugueseRegion`, business-day calculation |
| `Brasa.Shared` — persistence base | ✅ | `Entity` (UUIDv7), `ITenantOwned`, `IAuditable`, `ISoftDeletable` |
| `Brasa.Shared` — outbox contracts | ✅ | Types defined; **no dispatcher implementation yet** |
| `Brasa.Api` | ✅ | I0 walking skeleton: `/api/v1/ping`, `/menu`, `/orders` (+`/lines`, `/split`, `/close`), `/health`. Serilog, ProblemDetails, API versioning, idempotency, CORS for web clients (`Cors:AllowedOrigins`) |
| EF Core + PostgreSQL + RLS | ✅ | **Verified live**, not just asserted: `brasa_app` (unprivileged runtime role) sees zero rows with no tenant set or the wrong tenant set, and cannot run DDL. See [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| `Modules.Identity` | 📁 | I3 (auth) |
| `Modules.Catalog` | ✅ | `MenuCategory`, `MenuItem`, seeded demo menu spanning both VAT bands, soft delete (CAT-18) |
| `Modules.Ordering` | ✅ | `Order` aggregate — open, add line (price/VAT snapshot), even split, close |
| `Modules.Fiscal` | ✅ | `IFiscalProvider`, `FiscalDocument`, VAT correctly derived from gross (menu prices are VAT-inclusive) |
| `Modules.Payments` | 📁 | I6 |
| `Modules.Reporting` | 📁 | I8 |
| `Fiscal.Portugal` | 📁 | **Nothing fiscal is implemented.** I7 |
| `Fiscal.Mock` | ✅ | Deterministic, `MOCK-`-prefixed output, production guard enforced at DI registration |

## Site Agent

| Component | State | Notes |
|---|---|---|
| `Brasa.SiteAgent` | 🚧 | Host starts and stops cleanly. Nothing else — Month 3 |
| Fiscal signing | ⬜ | |
| ESC/POS printing | ⬜ | |
| LAN REST + SignalR hub | ⬜ | |
| Cloud outbox sync | ⬜ | |

## Web clients

| Client | State | Notes |
|---|---|---|
| `pos` | ✅ I0 shell | React 19 + Vite 8 + TS, one screen: open table → menu → lines → split preview → close → receipt. pt-PT default / en toggle, cookie-persisted (ADR 0011). No auth, no offline, no Dexie yet — those are I2 (see [roadmap.md](roadmap.md)) |
| `kds` | ⬜ | |
| `admin` | ⬜ | |
| `order` (QR self-ordering) | ⬜ | |

## Tests

| Suite | State | Notes |
|---|---|---|
| `Brasa.Shared.Tests` | ✅ | 17 passing, incl. exhaustive allocation check |
| `Brasa.Fiscal.Portugal.Tests` | ✅ | 13 passing: gross→net VAT derivation (exhaustive per rate), mock provider sequential numbering, mixed-rate reconciliation |
| `Brasa.Api.IntegrationTests` | 📁 | Testcontainers referenced and Docker available; no tests written yet |
| E2E (Playwright) | ✅ | `src/web/e2e` — 9 tests, all green (verified warm and from a cold start): full UI walking-skeleton (QA-05), API-level split-math sweep (QA-03), language toggle + cookie persistence (WEB-13). CI job written but **not yet run in CI**. See [../development/e2e-testing.md](../development/e2e-testing.md) |

## I0 demo — verified live, not just unit-tested

Run end-to-end against the real API and a real PostgreSQL container on
2026-08-09: open a table → add 2× Frango na Brasa (13%) + 2× Imperial (23%) →
preview an even 3-way split (7.54/7.53/7.53, summing to the cent) → close →
receive a fiscal document whose gross total matches the order total shown
throughout service, with net + VAT reconciling to the cent. Idempotency
verified by replaying an identical `POST /orders` and confirming, via direct
SQL, that only one row was created.

The same flow was then re-verified through the actual HTTP path the `pos` web
shell uses — CORS preflight, `Origin: http://localhost:5173`, and the
`Idempotency-Key` header on every mutating call — confirming the API's JSON
shapes match the shell's TypeScript types field-for-field and that a missing
`Idempotency-Key` returns the `ProblemDetails.code` shape the client parses.

> **Update (i18n):** `pos` now defaults to Portuguese with an English toggle,
> preference persisted in a cookie (never sent to the API) behind a storage
> interface written so a future mobile client swaps in `AsyncStorage` instead
> — see [ADR 0011](../architecture/decisions/0011-i18n.md). Money and the
> receipt's issued date deliberately stay `pt-PT` regardless of the toggle —
> fiscal formatting is not UI chrome. Server-sent error text is **not** yet
> localized (known gap, documented in the ADR, not hidden).

> **Update:** the browser gap noted above has since been closed. A Playwright
> harness (`src/web/e2e`) now drives the actual rendered `pos` UI in a real
> Chromium instance — click the table-open form, click real menu buttons,
> read the rendered total and split amounts, close, read the rendered
> receipt — and passes, verified twice: once against already-running dev
> servers, once from a hard cold start (both processes killed, Playwright's
> `webServer` config launching `dotnet run` and `npm run dev` itself). See
> [../development/e2e-testing.md](../development/e2e-testing.md). The one
> piece still unverified is the CI job itself — written, not yet exercised by
> an actual GitHub Actions run.

Three real bugs were found and fixed by this live run — none were caught by
`dotnet build` or the pre-existing unit tests:

1. **RLS was a complete no-op.** The bootstrap Postgres role is a superuser,
   and superusers bypass row-level security unconditionally regardless of
   `FORCE ROW LEVEL SECURITY`. Fixed by splitting into an unprivileged runtime
   role and a migration-only owner role. See
   [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md).
2. **New order lines were tracked `Modified` instead of `Added`.** EF Core's
   default `Guid` key convention assumes a non-default key on a
   navigation-discovered entity means it already exists. Fixed with
   `ValueGeneratedNever()` — see the comment on `ApplyEntityConventions` in
   `Brasa.Shared/Persistence/ModelBuilderExtensions.cs`.
3. **VAT was computed backwards.** Portuguese menu prices are VAT-inclusive;
   the fiscal document must derive net/VAT from the gross price, not add VAT
   on top. See [docs/fiscal/README.md](../fiscal/README.md#menu-prices-are-vat-inclusive).

## Mobile readiness

Android and iOS apps follow shortly after the web launch and must need **no
backend change**. These are the seams that make that true — all design-only so
far. Rules: [../architecture/api-contract.md](../architecture/api-contract.md).

| # | Seam | State | Retrofit cost if skipped |
|---|---|---|---|
| 1 | Token auth (OAuth 2.1 + PKCE), device-bound rotating refresh | ⬜ | **Highest** — tearing out cookie auth, re-authenticating every client |
| 2 | Device registry + terminal pairing | ⬜ | High — push tokens, revocation and terminal identity all hang off it |
| 3 | Cursor-based sync endpoints | ⬜ | High — changing the protocol breaks every offline client at once |
| 4 | Idempotency keys on all mutations | ⬜ | High — auditing every mutation for double-effect |
| 5 | `X-Brasa-Client` header + `client-requirements` endpoint | ⬜ | Medium — without it the first mobile release can never be safely deprecated |
| 6 | Stable error-code contract | 🚧 | Medium — `Error.Code` exists; the stability guarantee is not yet enforced |
| 7 | Push token registration + `IPushChannel` | ⬜ | Low — a migration and an endpoint |
| 8 | OpenAPI generation + breaking-change CI | ⬜ | Low now, but it is what keeps 1–7 honest |

Planned clients: staff handheld, owner dashboard, customer app, native KDS.
Stack undecided — the API stays client-agnostic so any of React Native, Flutter
or native Kotlin/Swift remains open.

## Infrastructure

| Component | State | Notes |
|---|---|---|
| Docker | ✅ | 29.6.2, Compose v5.3.1 |
| PostgreSQL | ✅ | 18.4 container, ICU locale provider, `pt-PT` |
| Seq (log viewer) | ✅ | `http://localhost:5341` |
| CI (GitHub Actions) | ✅ | Build gate, tests, vulnerability scan, docs link check. `e2e` job added this session — written and locally-equivalent to what passed on this machine, but not yet exercised by an actual CI run |

## Blockers

| # | Blocker | Impact | Owner |
|---|---|---|---|
| 1 | Portuguese legal entity not yet formed | Cannot submit Modelo 24 to AT. **Start now — it is on the critical path for revenue, not for code** | Founder |
| 2 | VAT rules unconfirmed by an accountant | `TaxRule` design absorbs any answer, but rates must be verified before launch | Founder |
| 3 | `Brasa` trademark and domains not cleared | Check INPI (PT), EUIPO (classes 9/42), and `.pt`/`.com` before spending on branding | Founder |
