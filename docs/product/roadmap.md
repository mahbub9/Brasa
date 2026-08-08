# Roadmap — incremental delivery

**The plan of *when*.** [backlog.md](backlog.md) is the plan of *what*, and
holds the authoritative task status. This page sequences those tasks into
increments, each ending in something demonstrable.

**Last updated:** 2026-08-08 · Methodology:
[ADR 0009](../architecture/decisions/0009-incremental-delivery.md)

## Targets

| When | Deliverable | Meaning |
|---|---|---|
| **Week 1** | Walking skeleton | A demo you can drive end to end, deployed, not on localhost |
| **Month 1** | Pilot-ready | Real people can use it daily against demo data |
| **Month 3** | Working solution | Feature-complete enough to run a real restaurant, in parallel-run |
| Month 6 | Certified and live | AT certificate issued; a real restaurant invoices on it |

## The one thing incremental delivery cannot change

**Certification is binary.** There is no half-certified state — a restaurant may
not issue real fiscal documents on uncertified software, whatever the software
can do. AT's review is 30 days, suspended during conformity tests, and assumes at
least one remediation cycle.

So the Month 3 target is *"working solution"*, not *"paying customers"*. The two
are separated by the certification gate, which the schedule cannot compress.

**This does not block piloting.** Real restaurants can run Brasa from Month 1
onwards in **parallel-run mode** — real orders, real kitchen flow, real staff
using it during service — while fiscal documents continue to be issued by their
existing certified system. That is legal, needs no certificate, and produces the
feedback that matters. See
[ADR 0002](../architecture/decisions/0002-own-fiscal-engine.md).

## Definition of done, per increment

An increment is finished when it can be **demonstrated**, not when the code
compiles. Each below carries a demo script; if the script cannot be run start to
finish, the increment is not done.

Every increment also stays deployable. Shipping weekly is impossible without a
pipeline, which is why deployment lands in I0 rather than at the end.

---

## I0 — Walking skeleton · Week 1

**Goal:** the thinnest possible slice through every layer — browser → API →
PostgreSQL → back — deployed and demoable.

**Demo:** open a table, add three items, see the running total, split the bill
three ways, produce a receipt. In a browser, on a real URL.

| Tasks |
|---|
| DAT-01 EF Core + Npgsql · DAT-03 Money mapping · DAT-04 query filters · **DAT-05 RLS** · DAT-06 session variable · DAT-10 initial migration |
| API-01 `/api/v1` versioning · API-03 ProblemDetails · API-05 idempotency |
| CAT-01/02 categories and items (seeded, no CRUD UI) · CAT-07 minimal `TaxRule` |
| ORD-01/02/03/04 order aggregate, open table, lines, price snapshots · ORD-15 even split |
| FIS-01 `IFiscalProvider` · FIS-02 `Fiscal.Mock` · FIS-03 production guard |
| WEB minimal POS shell — one screen, no offline |
| OPS-11 deployment (Hetzner + Caddy + Compose) |

**Deliberately not included:** authentication, offline, printing, real fiscal,
menu editing, KDS.

> **RLS is in I0 on purpose.** Adding a row-level security policy alongside the
> first table costs nothing and becomes a habit. Retrofitting it even one
> increment later means auditing every query already written. The same logic
> applies to idempotency keys and `/api/v1` — they are conventions, not
> features, and conventions are free only if they start at line one.

## I1 — Menu and floor · Week 2

**Goal:** a restaurant's own menu and room, entered by them rather than seeded.

**Demo:** build a menu with categories, modifiers and prices in the back-office;
lay out a dining room; take an order against it.

CAT-03/04 modifiers · CAT-05 price lists · CAT-08 VAT resolution · CAT-09
alcohol band · CAT-13 86-ing · CAT-18 soft delete · FLR-01…04 rooms, tables,
editor, states · WEB back-office shell

## I2 — Real service flow · Week 3

**Goal:** a waiter's actual working day, minus the kitchen.

**Demo:** seat a party, fire starters then mains, move them to another table,
add a round, void an item, split by item, print a pre-bill.

ORD-05…14 modifiers, notes, courses, firing, voids, discounts, transfers,
merges · ORD-16/17 split by item and cover · ORD-18/19 pre-bill · ORD-20
takeaway · ORD-22 history

## I3 — Auth and multi-user · Week 4

**Goal:** more than one person, safely. This is the gate before external users.

**Demo:** pair a tablet, sign in with a PIN, take an order as one waiter and a
manager-authorised void as another, revoke a lost device.

IDN-01…11 hierarchy, accounts, PKCE, tokens, device registry, pairing, PIN,
roles, manager authorisation · IDN-13 provisioning · IDN-16 feature flags ·
API-06/07 client version negotiation

> **Month 1 checkpoint — pilot-ready.** Real people can use it daily.
> Start parallel-run with a friendly restaurant here, and start the
> [validation interviews](differentiation.md#validation-plan).

## I4 — Kitchen · Weeks 5–6

**Demo:** fire a course; a ticket prints at the grill and the bar; the KDS shows
it ageing; bump it.

KIT-01…09 ESC/POS, transports, routing, retry, drawer · KIT-10…13 KDS ·
KIT-14 hardware shortlist · AGT-01…07 Site Agent store, pairing, LAN API, hub,
sync

## I5 — Offline · Weeks 7–8

**Goal:** the core product promise.

**Demo:** pull the network cable mid-service. Keep taking orders and payments.
Restore it. Everything syncs once, no duplicates, no gaps.

SYN-01…13 outbox, cursor sync, conflicts, LAN-first, chaos tests ·
AGT-11/12/14 health, deployment, degraded mode · QA-06 offline E2E suite

## I6 — Payments and cash · Week 9

**Demo:** split a bill across cash and card, add a tip, open and close a cash
session with a blind count and variance.

PAY-01…12 tenders, splits, tips, refunds, cash sessions, meal vouchers

> **Month 2 checkpoint.** Everything except fiscal is real.

## I7 — Fiscal engine · Weeks 10–11

⚠️ Certification-relevant. Read [../fiscal/README.md](../fiscal/README.md) first.

**Demo:** issue a *fatura simplificada* with a valid ATCUD and a QR that scans;
issue a credit note; break the chain deliberately and watch verification catch it.

FIS-04…20 series, AT registration, ATCUD, signing, chain, numbering, QR,
document types, immutability, audit, verification · FIS-24 golden files ·
AGT-08/09/10 key custody, offline signing, crash-safe counters

## I8 — Reports and SAF-T · Week 12

**Demo:** run a Z report that reconciles to the cent against the day's fiscal
documents; export SAF-T and validate it against AT's XSD.

RPT-01…12 read models, X/Z, breakdowns, VAT summary, business-day boundaries ·
FIS-21/22/23 SAF-T export, XSD validation in CI, submission job

> **Month 3 checkpoint — working solution.** Feature-complete enough to run a
> restaurant, in parallel-run. Certification not yet held.

## After Month 3

Ordered by what unblocks revenue:

1. **Certification** — submit Modelo 24 at the *start* of a month, not the end.
   Requires the Portuguese entity, which must already exist by then.
2. **Hardening** — load testing, backup and restore drills, security review.
3. **QR self-ordering** (QR-01…08) — high commercial value, no compliance
   dependency, which is exactly why it is safe to defer.
4. **Mobile apps** (MOB) — the backend seams are already built, so this adds no
   backend work.
5. **Differentiators** (DIF) — only what interviews validated.

## How features get deferred safely

Not every feature ships in the increment that builds it. Per-tenant,
per-platform feature flags (IDN-16) land in I3, so work can merge to `main`
continuously and be revealed per tenant when it is ready.

This is what makes "develop gradually" compatible with "ship weekly": the
branch is never long-lived, the flag is.

## Honest risks

| Risk | Mitigation |
|---|---|
| **I0 in one week is tight.** It touches seven layers | Cut to a single hardcoded tenant and no back-office UI. Do not cut RLS or idempotency |
| **Weekly demos crowd out testing** | E2E harness lands alongside I0–I1, not after. A demo that only works when driven by hand is not a demo |
| **Fiscal is late in the schedule** | Deliberate — it is the least changeable code, and building it last means building it against a stable domain. But it cannot slip past I7 without threatening certification |
| **Parallel-run needs a willing restaurant** | Start that conversation in Month 1, not Month 3 |
| **Increment scope creeps** | The demo script is the contract. If it runs, the increment is done; new ideas go to the backlog |
