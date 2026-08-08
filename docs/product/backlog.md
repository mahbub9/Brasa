# Backlog

The plan of record. Every feature and task, with a stable ID and a status.

**How this relates to the other product docs**

| Doc | Answers |
|---|---|
| **This page** | What are we building, and how far along is each task? |
| [roadmap.md](roadmap.md) | **When** — which increment each task belongs to, and its demo |
| [status.md](status.md) | Which code actually exists and works today? |
| [../features/](../features/) | How does a built feature behave — offline, on failure? |
| [plan.md](plan.md) | Why is the roadmap shaped this way? (historical record) |

> **Delivery is incremental** — vertical slices, each ending in a runnable demo,
> not layer-by-layer. The epic tables below are grouped by area for reference;
> **[roadmap.md](roadmap.md) is what says what to build next.** Tasks from many
> epics land in the same week.

**Rules**

- IDs are **stable and never reused**. Reference them in commits and issues:
  `feat(identity): terminal pairing (IDN-06)`.
- Update the status in the **same commit** as the work. A backlog that lags is
  worse than none, because it is trusted.
- New work gets the next free ID in its epic. Never renumber.

**Legend** ✅ done · 🚧 in progress · ⬜ todo · 🔒 blocked · ⏭ deferred past MVP

**Last updated:** 2026-08-09

---

## Progress

| Epic | Area | Done | Total | Phase |
|---|---|---:|---:|---|
| **FND** | Foundation & shared kernel | 10 | 12 | I0 |
| **OPS** | Infrastructure, CI, observability | 6 | 16 | I0 → ongoing |
| **DOC** | Documentation system | 9 | 10 | I0 → ongoing |
| **API** | API platform & mobile readiness | 3 | 18 | I0 (rest: I3) |
| **DAT** | Persistence, tenancy, RLS | 9 | 11 | I0 |
| **IDN** | Identity & access | 0 | 16 | I3 |
| **CAT** | Catalog & menu | 3 | 18 | I0 (rest: I1) |
| **FLR** | Floor plan & tables | 0 | 7 | I1 |
| **ORD** | Ordering | 5 | 22 | I0 (rest: I2) |
| **SYN** | Offline sync engine | 0 | 13 | I5 |
| **AGT** | Site Agent | 0 | 15 | I4–I5 |
| **KIT** | Kitchen printing & KDS | 0 | 14 | I4 |
| **FIS** | Fiscal engine | 3 | 24 | I0 (rest: I7) |
| **WEB** | Web clients | 2 | 13 | I0 (rest: I1–I8) |
| **PAY** | Payments & cash sessions | 0 | 14 | I6 |
| **RPT** | Reporting | 0 | 12 | I8 |
| **QR** | QR self-ordering | 0 | 9 | Post-I8 |
| **QA** | Automated testing | 3 | 14 | I0–I1 → ongoing |
| **MOB** | Mobile apps | 0 | 12 | Post-launch |
| **DIF** | Differentiators | 0 | 21 | Post-MVP — see [differentiation.md](differentiation.md) |
| | **Total** | **53** | **291** | |

> Phase labels now follow the increments in [roadmap.md](roadmap.md) (I0…I8),
> not the original Month-based sequencing — see
> [ADR 0009](../architecture/decisions/0009-incremental-delivery.md).
>
> 53 of 291 — I0's backend, the `pos` web shell (now with pt/en i18n), and a
> first Playwright E2E harness are done and proven against a live API (details:
> [status.md](status.md#i0-demo-verified-live-not-just-unit-tested)). Every
> epic marked "I0 (rest: …)" is intentionally partial: I0 builds only the
> single vertical slice the walking-skeleton demo needs, not a whole epic.

---

## FND — Foundation & shared kernel

| ID | Task | Status |
|---|---|---|
| FND-01 | .NET 10 solution, 14 projects, modular monolith structure | ✅ |
| FND-02 | Central package management, zero-warning build policy | ✅ |
| FND-03 | `Money` — integer minor units, allocation-based splitting | ✅ |
| FND-04 | `Result` / `Error` — expected failures as values | ✅ |
| FND-05 | `ITenantContext` / `TenantContext` — resolve once per scope | ✅ |
| FND-06 | `IClock`, `PortugueseRegion`, business-day calculation | ✅ |
| FND-07 | `Entity` base — UUIDv7, tenant-owned, auditable, soft-delete | ✅ |
| FND-08 | Integration event and outbox **contracts** | ✅ |
| FND-09 | API host bootstrap — Serilog, ProblemDetails, health | ✅ |
| FND-10 | Site Agent worker host | ✅ |
| FND-11 | In-process integration event **dispatcher** implementation | ⬜ |
| FND-12 | Outbox processor — polling, retry, backoff, poison handling | ⬜ |

## DAT — Persistence, tenancy, RLS

| ID | Task | Status |
|---|---|---|
| DAT-01 | EF Core + Npgsql wiring, connection resilience | ✅ |
| DAT-02 | Schema-per-module conventions | ✅ `catalog` / `ordering` schemas |
| DAT-03 | `Money` value converter / owned type mapping | ✅ `MapMoney` |
| DAT-04 | Global query filter for `ITenantOwned` | ✅ |
| DAT-05 | PostgreSQL RLS policies on every tenant-owned table | ✅ **verified live** — see [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| DAT-06 | Session variable set from `ITenantContext` per request | ✅ `TenantSessionInterceptor` |
| DAT-07 | Privileged role + `ResolveAsSystem()` path for background jobs | ⬜ flag exists; connection path not designed — see [multi-tenancy.md](../architecture/multi-tenancy.md#the-system-context) |
| DAT-08 | Audit interceptor — `CreatedAt/By`, `ModifiedAt/By` | ✅ `TenantAwareDbContext.StampEntities` |
| DAT-09 | `AssignTenant` interceptor on insert | ✅ |
| DAT-10 | Initial migration + migration-on-startup policy | ✅ migrate-on-startup, elevated role only |
| DAT-11 | Reflection test: every entity is `ITenantOwned` or allow-listed | ⬜ |

## API — API platform & mobile readiness

> Rules: [../architecture/api-contract.md](../architecture/api-contract.md).
> These are the seams that let Android and iOS ship with no backend change.

| ID | Task | Status |
|---|---|---|
| API-01 | `/api/v1` versioning via `Asp.Versioning` | ✅ literal prefix + `ApiVersionSet`, not a templated segment — see commit message for why |
| API-02 | `/api/public/v1` consumer surface, separated from tenant API | ⬜ |
| API-03 | ProblemDetails mapping from `ErrorType` → HTTP status | ✅ |
| API-04 | Stable error-code registry + test that codes never change meaning | ⬜ codes exist (`order.already_closed` etc.); no stability test yet |
| API-05 | `Idempotency-Key` middleware + store | ✅ **verified live**: replayed request returns identical order id; DB confirms one row. In-memory store — durable store needed before scaling out |
| API-06 | `X-Brasa-Client` header parsing (id / version / platform) | ⬜ |
| API-07 | `GET /client-requirements` — min & recommended version, sunset | ⬜ |
| API-08 | RFC 8594 `Deprecation` / `Sunset` response headers | ⬜ |
| API-09 | Cursor pagination helper, applied to every collection | ⬜ |
| API-10 | `ETag` / `If-None-Match` on config and menu reads | ⬜ |
| API-11 | Response compression | ⬜ |
| API-12 | Rate limiting, keyed by client and tenant | ⬜ |
| API-13 | OpenAPI document generation, committed to the repo | ⬜ |
| API-14 | CI breaking-change detection against previous OpenAPI | ⬜ |
| API-15 | TypeScript SDK generation into `web/sdk` | ⬜ |
| API-16 | SignalR hub, JSON protocol | ⬜ |
| API-17 | REST equivalent for every realtime message (enforced by test) | ⬜ |
| API-18 | `/.well-known/` app-link documents for iOS and Android | ⬜ |

## IDN — Identity & access

| ID | Task | Status |
|---|---|---|
| IDN-01 | Organization / Site / Terminal hierarchy | ⬜ |
| IDN-02 | User accounts, email verification, password reset | ⬜ |
| IDN-03 | OAuth 2.1 / OIDC authorization-code flow with PKCE | ⬜ |
| IDN-04 | Access token (JWT) issuance and validation | ⬜ |
| IDN-05 | Refresh token — opaque, rotating, device-bound, replay detection | ⬜ |
| IDN-06 | Device registry — register, list, revoke individually | ⬜ |
| IDN-07 | Terminal pairing via short-lived device code | ⬜ |
| IDN-08 | Staff PIN sign-in on a paired terminal | ⬜ |
| IDN-09 | PIN hashing, lockout, and rotation policy | ⬜ |
| IDN-10 | Roles & permissions model | ⬜ |
| IDN-11 | Manager-authorisation flow for privileged actions (voids, discounts) | ⬜ |
| IDN-12 | Consumer identity realm for the public surface | ⬜ |
| IDN-13 | Tenant provisioning / onboarding | ⬜ |
| IDN-14 | Push token registration endpoints | ⬜ |
| IDN-15 | `IPushChannel` abstraction (no provider adapter yet) | ⬜ |
| IDN-16 | Per-tenant, per-platform feature flags | ⬜ |

## CAT — Catalog & menu

| ID | Task | Status |
|---|---|---|
| CAT-01 | Menu categories, ordering, visibility | ✅ |
| CAT-02 | Menu items — name, description, image, allergens | 🚧 name/price/VAT only; description/image/allergens are I1 |
| CAT-03 | Modifier groups (required / optional, min / max) | ⬜ |
| CAT-04 | Modifiers with price deltas | ⬜ |
| CAT-05 | Price lists per site | ⬜ |
| CAT-06 | Channel pricing — dine-in / takeaway / delivery | ⬜ |
| CAT-07 | `TaxRule` — item × channel × region, effective-dated | ⬜ `VatRate` ships as the explicit I0 placeholder — see its doc comment |
| CAT-08 | VAT resolution service with date-aware lookup | ⬜ |
| CAT-09 | Alcohol flag driving the 23% band separation | ✅ `MenuItem.IsAlcoholic` |
| CAT-10 | Combos / menus (*menu do dia*) | ⬜ |
| CAT-11 | *Prato do dia* — daily specials with schedules | ⬜ |
| CAT-12 | *Couvert* handling — charged only when consumed | ⬜ |
| CAT-13 | Item availability / 86-ing (out of stock) | ✅ `MarkAvailable`/`MarkUnavailable` |
| CAT-14 | Course assignment per item | ⬜ |
| CAT-15 | Kitchen station routing per item | ⬜ |
| CAT-16 | Menu versioning with effective dates | ⬜ |
| CAT-17 | Bulk import (CSV / Excel) | ⬜ |
| CAT-18 | Soft delete preserving historical order references | ⬜ |

## FLR — Floor plan & tables

| ID | Task | Status |
|---|---|---|
| FLR-01 | Rooms / areas (indoor, esplanada, bar) | ⬜ |
| FLR-02 | Tables — number, seats, position, shape | ⬜ |
| FLR-03 | Drag-and-drop floor plan editor | ⬜ |
| FLR-04 | Table states (free, occupied, bill requested, dirty) | ⬜ |
| FLR-05 | Table merge / split for large parties | ⬜ |
| FLR-06 | Section assignment to waiters | ⬜ |
| FLR-07 | Multi-floor support | ⬜ |

## ORD — Ordering

| ID | Task | Status |
|---|---|---|
| ORD-01 | Order aggregate — lifecycle and state machine | ✅ `Open`/`Closed`; richer states (courses, kitchen status) are I2 |
| ORD-02 | Open a table, set cover count | ✅ |
| ORD-03 | Add / remove / edit order lines | 🚧 add only — remove/edit are I2 |
| ORD-04 | Line snapshots — name, price, VAT rate at time of sale | ✅ |
| ORD-05 | Apply modifiers to a line | ⬜ |
| ORD-06 | Free-text kitchen notes | ⬜ |
| ORD-07 | Courses and course firing | ⬜ |
| ORD-08 | Send to kitchen (partial and full) | ⬜ |
| ORD-09 | Order line status tracking | ⬜ |
| ORD-10 | Void a line, with reason and manager authorisation | ⬜ |
| ORD-11 | Discounts — line, order, percentage and fixed | ⬜ |
| ORD-12 | Transfer table | ⬜ |
| ORD-13 | Transfer individual lines between tables | ⬜ |
| ORD-14 | Merge orders | ⬜ |
| ORD-15 | Split bill evenly (`Money.Allocate`) | ✅ **verified live**: 22.60 EUR → 7.54/7.53/7.53, sums to the cent |
| ORD-16 | Split bill by item | ⬜ |
| ORD-17 | Split bill by cover | ⬜ |
| ORD-18 | Pre-bill — *documento não fiscal*, correctly labelled | ⬜ |
| ORD-19 | Reprint pre-bill (must match the original exactly) | ⬜ |
| ORD-20 | Takeaway and counter-sale flow | ⬜ |
| ORD-21 | Order ownership + concurrent-terminal conflict protocol | ⬜ |
| ORD-22 | Order history and search | ⬜ |

## SYN — Offline sync engine

| ID | Task | Status |
|---|---|---|
| SYN-01 | Client outbox schema (IndexedDB / SQLite) | ⬜ |
| SYN-02 | `POST /sync/push` — idempotent mutation batch | ⬜ |
| SYN-03 | `GET /sync/pull` — cursor-based delta | ⬜ |
| SYN-04 | Opaque server-issued cursor (never a timestamp) | ⬜ |
| SYN-05 | Conflict resolution policy per entity type | ⬜ |
| SYN-06 | Client-side queue with retry and backoff | ⬜ |
| SYN-07 | Connectivity detection and mode switching | ⬜ |
| SYN-08 | LAN-first, cloud-fallback endpoint resolution | ⬜ |
| SYN-09 | Sync status UI — pending count, last sync, errors | ⬜ |
| SYN-10 | Initial full-sync / bootstrap for a new terminal | ⬜ |
| SYN-11 | Compaction of superseded local mutations | ⬜ |
| SYN-12 | Clock-skew tolerance | ⬜ |
| SYN-13 | Chaos tests — kill network mid-order, mid-payment, mid-print | ⬜ |

## AGT — Site Agent

| ID | Task | Status |
|---|---|---|
| AGT-01 | SQLite local store + EF Core model sharing | ⬜ |
| AGT-02 | Pairing flow with the cloud | ⬜ |
| AGT-03 | LAN REST API for terminals | ⬜ |
| AGT-04 | LAN SignalR hub | ⬜ |
| AGT-05 | mDNS / discovery so terminals find the agent | ⬜ |
| AGT-06 | Outbox sync to cloud | ⬜ |
| AGT-07 | Config pull from cloud | ⬜ |
| AGT-08 | Fiscal key custody + at-rest protection | ⬜ |
| AGT-09 | Offline document signing | ⬜ |
| AGT-10 | Series counter with crash-safe persistence | ⬜ |
| AGT-11 | Health endpoint and diagnostics | ⬜ |
| AGT-12 | Installer / deployment (Windows Service or container) | ⬜ |
| AGT-13 | Auto-update with version pinning | ⬜ |
| AGT-14 | Agent-down degraded mode (reserve series) | ⬜ |
| AGT-15 | Remote log shipping | ⬜ |

## KIT — Kitchen printing & KDS

| ID | Task | Status |
|---|---|---|
| KIT-01 | ESC/POS command builder | ⬜ |
| KIT-02 | TCP printer transport | ⬜ |
| KIT-03 | USB / serial printer transport | ⬜ |
| KIT-04 | Printer configuration and station mapping | ⬜ |
| KIT-05 | Ticket layout templates | ⬜ |
| KIT-06 | Station routing rules | ⬜ |
| KIT-07 | Print retry, queue, and failure surfacing on the POS | ⬜ |
| KIT-08 | Printer-down fallback rerouting | ⬜ |
| KIT-09 | Cash drawer kick | ⬜ |
| KIT-10 | KDS — station views | ⬜ |
| KIT-11 | KDS — bump and recall | ⬜ |
| KIT-12 | KDS — prep timers and colour-coded ageing | ⬜ |
| KIT-13 | KDS — course firing controls | ⬜ |
| KIT-14 | Verified hardware shortlist + test matrix | ⬜ |

## FIS — Fiscal engine

> ⚠️ Certification-relevant. Read [../fiscal/README.md](../fiscal/README.md)
> before starting any of these.

| ID | Task | Status |
|---|---|---|
| FIS-01 | `IFiscalProvider` abstraction | ✅ |
| FIS-02 | `Fiscal.Mock` deterministic provider | ✅ every value `MOCK-`-prefixed; VAT correctly derived from gross price |
| FIS-03 | Production guard — mock must never load in Production | ✅ enforced at DI registration, not just documented |
| FIS-04 | `FiscalSeries` entity and lifecycle | ⬜ |
| FIS-05 | AT webservice client — series registration | ⬜ |
| FIS-06 | Série validation code storage | ⬜ |
| FIS-07 | ATCUD generation | ⬜ |
| FIS-08 | RSA key loading and signing | ⬜ |
| FIS-09 | Signature chain — hash of previous document in series | ⬜ |
| FIS-10 | Gapless sequential numbering, crash-safe | ⬜ |
| FIS-11 | QR code payload to AT field spec | ⬜ |
| FIS-12 | QR rendering at ≥30×30mm | ⬜ |
| FIS-13 | Document type `FS` — fatura simplificada | ⬜ |
| FIS-14 | Document type `FT` — fatura with NIF | ⬜ |
| FIS-15 | Document type `FR` — fatura-recibo | ⬜ |
| FIS-16 | Document type `NC` — nota de crédito | ⬜ |
| FIS-17 | Non-fiscal document generation | ⬜ |
| FIS-18 | Immutability enforcement — no update path to issued documents | ⬜ |
| FIS-19 | Append-only fiscal audit log | ⬜ |
| FIS-20 | Chain verification job with alerting | ⬜ |
| FIS-21 | SAF-T (PT) XML export | ⬜ |
| FIS-22 | SAF-T XSD validation in CI | ⬜ |
| FIS-23 | Monthly SAF-T submission job (by the 5th) with retry and paging | ⬜ |
| FIS-24 | Golden-file test suite | ⬜ |

## WEB — Web clients

> React + TypeScript, PWA. `pos`, `kds`, `admin`, `order` each get their own
> app; `web/ui` and `web/sdk` are shared across all four. This epic covers the
> client shells themselves — the domain work they call into (menu, orders,
> floor plan) is tracked in its own epic (CAT, ORD, FLR, ...).

| ID | Task | Status |
|---|---|---|
| WEB-01 | `pos` minimal shell — open table, ring up, split preview, close | ✅ I0: one screen, no auth, no offline. See [status.md](status.md#web-clients) |
| WEB-02 | Shared `web/ui` component library | ⬜ |
| WEB-03 | Shared `web/sdk` — OpenAPI-generated typed client | ⬜ replaces `pos`'s hand-written `src/api/` (I0 placeholder) |
| WEB-04 | `pos` — Dexie local store and offline-first data layer | ⬜ I2, depends on SYN |
| WEB-05 | `pos` — floor plan / table selection screen | ⬜ depends on FLR |
| WEB-06 | `pos` — menu browsing with modifiers and courses | ⬜ |
| WEB-07 | `pos` — staff PIN login screen | ⬜ depends on IDN |
| WEB-08 | `kds` shell — station view, bump, prep timers | ⬜ I4 |
| WEB-09 | `admin` shell — back-office SPA scaffold | ⬜ I1 |
| WEB-10 | `admin` — menu and floor-plan editors | ⬜ |
| WEB-11 | `admin` — staff, roles and reporting screens | ⬜ |
| WEB-12 | `order` shell — QR self-ordering PWA | ⬜ Post-I8 |
| WEB-13 | i18n — pt default / en toggle, cookie-persisted, mobile storage seam | ✅ i18next, `src/i18n/`. See [ADR 0011](../architecture/decisions/0011-i18n.md) |

## PAY — Payments & cash sessions

| ID | Task | Status |
|---|---|---|
| PAY-01 | Payment / tender model | ⬜ |
| PAY-02 | Cash tender with change calculation | ⬜ |
| PAY-03 | Card tender, manually captured from a standalone TPA | ⬜ |
| PAY-04 | Split tender across multiple methods | ⬜ |
| PAY-05 | Partial payment against an open order | ⬜ |
| PAY-06 | Tips — recording and attribution | ⬜ |
| PAY-07 | Refunds via credit note | ⬜ |
| PAY-08 | Cash session — *abertura de caixa* with float | ⬜ |
| PAY-09 | Cash movements — pay-in, pay-out, with reason | ⬜ |
| PAY-10 | Blind cash count | ⬜ |
| PAY-11 | *Fecho de caixa* with variance reporting | ⬜ |
| PAY-12 | Meal-voucher tenders (Ticket Restaurante, Edenred, Sodexo) | ⬜ |
| PAY-13 | MB WAY / Multibanco references via Ifthenpay or Easypay | ⏭ |
| PAY-14 | Integrated TPA (SIBS, Unicre, SumUp, myPOS) | ⏭ |

## RPT — Reporting

| ID | Task | Status |
|---|---|---|
| RPT-01 | Reporting read-model schema, isolated from transactional tables | ⬜ |
| RPT-02 | X report — mid-shift snapshot | ⬜ |
| RPT-03 | Z report — daily close, reconciling to the cent | ⬜ |
| RPT-04 | Sales by item | ⬜ |
| RPT-05 | Sales by category | ⬜ |
| RPT-06 | Sales by hour / daypart | ⬜ |
| RPT-07 | Sales by staff member | ⬜ |
| RPT-08 | VAT summary by rate | ⬜ |
| RPT-09 | Payment method breakdown | ⬜ |
| RPT-10 | Void and discount report | ⬜ |
| RPT-11 | SAF-T download from the back-office | ⬜ |
| RPT-12 | Business-day boundary handling (regional, incl. Azores) | ⬜ |

## QR — QR self-ordering

| ID | Task | Status |
|---|---|---|
| QR-01 | Per-table QR code generation | ⬜ |
| QR-02 | Public menu browsing (no account) | ⬜ |
| QR-03 | Guest cart and order submission | ⬜ |
| QR-04 | Route guest order → cloud → Site Agent | ⬜ |
| QR-05 | Staff approval before firing to kitchen | ⬜ |
| QR-06 | Order status for the guest | ⬜ |
| QR-07 | Allergen and dietary filtering | ⬜ |
| QR-08 | Multi-language guest UI (pt / en / es / fr) | ⬜ |
| QR-09 | Guest payment | ⏭ |

## QA — Automated testing

> See [../development/e2e-testing.md](../development/e2e-testing.md) for the
> harness itself and an honest account of what QA-04/06/07/08 are still
> blocked on.

| ID | Task | Status |
|---|---|---|
| QA-01 | Choose an E2E framework (Playwright vs alternatives) | ✅ Playwright + TypeScript, `src/web/e2e` |
| QA-02 | E2E harness — app + Postgres + seeded tenant | 🚧 `webServer` starts API + `pos` fresh each run; database is the persistent dev instance, not disposable per run |
| QA-03 | Deterministic test data builders | ✅ `tests/support/api.ts` — looks menu items up by name, never by id |
| QA-04 | Fixed-clock control for time-dependent tests | ⬜ nothing built yet needs it — see e2e-testing.md |
| QA-05 | E2E: full service loop, seat → order → fire → pay → close | ✅ `walking-skeleton.spec.ts` — open → order → split → close → receipt, driving the real UI. "Fire" and "pay" aren't built yet (KIT/PAY), so the loop ends at close |
| QA-06 | E2E: offline mode — network killed mid-service | ⬜ blocked on WEB-04/SYN — no offline capability exists to test |
| QA-07 | E2E: split-bill flows | 🚧 even split covered (`split-preview.spec.ts`, API-level, sweeps 1/2/3/5/7 ways); by-item/by-cover blocked on ORD-16/17 |
| QA-08 | E2E: multi-terminal concurrency | ⬜ blocked on ORD-21 |
| QA-09 | Testcontainers integration-test base fixture | ⬜ |
| QA-10 | Tenant isolation test suite (RLS) | ⬜ |
| QA-11 | Idempotency replay test harness | ⬜ |
| QA-12 | Fiscal golden-file infrastructure | ⬜ |
| QA-13 | Load test — 50 sites × 5 terminals at service rates | ⬜ |
| QA-14 | Accessibility checks on POS and guest UIs | ⬜ |

## OPS — Infrastructure, CI, observability

| ID | Task | Status |
|---|---|---|
| OPS-01 | Docker Compose — PostgreSQL 18 (ICU pt-PT) + Seq | ✅ |
| OPS-02 | CI — build gate, tests, vulnerability scan | ✅ |
| OPS-03 | CI — documentation link checking | ✅ |
| OPS-04 | Docs site published to GitHub Pages | ✅ |
| OPS-05 | `.gitattributes` line-ending normalisation | ✅ |
| OPS-06 | Issue and PR templates | ✅ |
| OPS-07 | Structured logging with tenant / site / terminal enrichment | ⬜ |
| OPS-08 | OpenTelemetry traces and metrics | ⬜ |
| OPS-09 | Health and readiness probes including the database | ⬜ |
| OPS-10 | Hangfire setup and dashboard | ⬜ |
| OPS-11 | Production deployment (Hetzner + Caddy) | ⬜ |
| OPS-12 | Automated database backup and a tested restore drill | ⬜ |
| OPS-13 | Secret management | ⬜ |
| OPS-14 | Error tracking (Sentry) for web clients | ⬜ |
| OPS-15 | Uptime and SAF-T submission alerting | ⬜ |
| OPS-16 | Staging environment | ⬜ |

## DOC — Documentation system

| ID | Task | Status |
|---|---|---|
| DOC-01 | Architecture overview and three-tier design | ✅ |
| DOC-02 | Fiscal reference — ATCUD, signature, QR, SAF-T | ✅ |
| DOC-03 | ADRs 0001–0008 with an index | ✅ |
| DOC-04 | AI session brief + repo map | ✅ |
| DOC-05 | Glossary of Portuguese fiscal and restaurant terms | ✅ |
| DOC-06 | Documentation contract and PR checklist | ✅ |
| DOC-07 | Feature page template | ✅ |
| DOC-08 | API contract for multi-platform clients | ✅ |
| DOC-09 | Backlog and progress tracking (this page) | ✅ |
| DOC-10 | Per-feature pages, written as features land | 🚧 |

## MOB — Mobile apps

> Post-launch. The backend seams (API-01…18, IDN-03…07) are what make these
> require **no backend change**.

| ID | Task | Status |
|---|---|---|
| MOB-01 | Choose the mobile stack | ⬜ |
| MOB-02 | Generate the platform API client from OpenAPI | ⬜ |
| MOB-03 | Staff handheld — ordering | ⬜ |
| MOB-04 | Staff handheld — offline engine | ⬜ |
| MOB-05 | Staff handheld — LAN discovery of the Site Agent | ⬜ |
| MOB-06 | Owner dashboard app | ⬜ |
| MOB-07 | Customer app — menu, ordering, loyalty | ⬜ |
| MOB-08 | Native KDS app | ⬜ |
| MOB-09 | APNs push adapter | ⬜ |
| MOB-10 | FCM push adapter | ⬜ |
| MOB-11 | Deep linking / universal links | ⬜ |
| MOB-12 | App Store and Play Store release pipelines | ⬜ |

## DIF — Differentiators

> Rationale, market analysis and validation status:
> **[differentiation.md](differentiation.md)**. Nothing here should be built
> before it is validated with real restaurants.

| ID | Task | Status |
|---|---|---|
| DIF-01 | Accountant portal — read-only tenant access for the *contabilista* | ⬜ |
| DIF-02 | Automated SAF-T delivery direct to the accountant | ⬜ |
| DIF-03 | Compliance dashboard — what is filed, what is missing, what is due | ⬜ |
| DIF-04 | VAT rate change auto-application via effective dates | ⬜ |
| DIF-05 | Migration importer — Zone Soft | ⬜ |
| DIF-06 | Migration importer — WinRest | ⬜ |
| DIF-07 | Parallel-run mode alongside an incumbent system | ⬜ |
| DIF-08 | Recipe and ingredient costing | ⬜ |
| DIF-09 | Live margin per dish | ⬜ |
| DIF-10 | Supplier price tracking with margin alerts | ⬜ |
| DIF-11 | True margin by channel, including aggregator commission | ⬜ |
| DIF-12 | Menu engineering classification (star / plowhorse / puzzle / dog) | ⬜ |
| DIF-13 | Void and discount anomaly detection (shrinkage) | ⬜ |
| DIF-14 | Demand forecasting for prep quantities | ⬜ |
| DIF-15 | Staff scheduling driven by forecast demand | ⬜ |
| DIF-16 | Waste tracking and reporting | ⬜ |
| DIF-17 | Natural-language reporting for owners | ⬜ |
| DIF-18 | Direct reservations and waitlist (TheFork commission alternative) | ⬜ |
| DIF-19 | Offline-capability proof — a visible, demonstrable guarantee | ⬜ |
| DIF-20 | Order-entry speed as a tracked product metric | ⬜ |
| DIF-21 | Accounting integrations — Primavera, Sage, PHC, Moloni | ⬜ |
