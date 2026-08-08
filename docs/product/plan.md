# Restaurant Management SaaS for Portugal — Build Plan

> **This is the plan as approved on 2026-08-08.** It is kept as a record; where
> reality has moved on, the living documents win. Current build state:
> [status.md](status.md).
>
> **Amendment, 2026-08-08 — multi-platform clients.** Android and iOS apps are
> now planned shortly after the web launch (staff handheld, owner dashboard,
> customer app, native KDS), and must ship with **no backend change**. This adds
> mobile-readiness seams to the Month 0–1 foundation work — token auth with
> PKCE, a device registry, cursor-based sync, client version negotiation, and
> OpenAPI breaking-change detection in CI. See
> [../architecture/api-contract.md](../architecture/api-contract.md),
> [ADR 0007](../architecture/decisions/0007-client-agnostic-api.md) and
> [ADR 0008](../architecture/decisions/0008-token-auth-no-cookies.md).
>
> The project was also renamed **Brasa**; paths below that read `RestaurantPos`
> are historical.

## Context

You are building a multi-tenant restaurant management SaaS targeting Portuguese restaurants — small first, medium and large later. The working directory is empty; this is greenfield.

The dominant constraint is **not** product features — it is Portuguese fiscal law. Invoicing software used in Portugal must be certified by the Autoridade Tributária (AT) under Portaria 363/2010. Non-certified software carries fines of €3,000–€18,750 per infraction, and mandatory use applies to any business over €50,000 turnover or issuing over 1,000 invoices/year — which is effectively every restaurant you want as a customer. Certification is granted to the **software producer**, so it gates your ability to sell, not your ability to build.

**Decisions taken:**

| Decision | Choice |
|---|---|
| Fiscal strategy | Build own AT-certified engine, behind an `IFiscalProvider` abstraction, with a mock provider for development |
| POS client | Device-agnostic offline-capable Web PWA (Android tablets, iPads, Windows touch terminals) |
| Backend stack | C# / .NET (your strongest language) |
| Team | Solo developer, full-time |
| Target | First real restaurant live in ~6 months |
| Legal entity | Not yet formed — will be set up |
| MVP scope | Core POS + KDS + kitchen/bar ESC/POS printing + QR self-ordering |

**Intended outcome:** a certified, offline-resilient, multi-tenant restaurant platform where the fiscal engine is the moat — it is precisely the barrier that keeps casual competitors out of the Portuguese market, and it is reusable as the template for later expansion into Spain, France, and Italy, which have equivalent fiscalisation regimes.

### One honest concern, stated up front

Six months solo for *all* of the above **plus** certification is aggressive. The plan below is sequenced so that if you slip, you cut from the end (QR self-ordering, then advanced reporting) rather than from the middle. The fiscal engine and the certification application must not slip, because they gate revenue. If you reach Month 4 behind schedule, drop QR self-ordering to post-launch — it is the only MVP item with no compliance or operational dependency.

---

## Architecture

### The central problem: offline + fiscal signing + thermal printing

Three requirements collide in a browser-based POS:

1. A restaurant's internet **will** fail, and service cannot stop when it does.
2. Portuguese fiscal documents require a chained RSA signature over a gapless per-series sequence — so signing must be possible without connectivity, but a private key cannot live safely in a browser.
3. A PWA cannot open raw TCP sockets, so it cannot drive ESC/POS kitchen printers or a cash drawer.

All three are solved by one component.

### Three-tier topology

```
┌─────────────────────── CLOUD (Azure/Hetzner, EU region) ────────────────────┐
│  ASP.NET Core API  ·  PostgreSQL  ·  SignalR  ·  Hangfire                    │
│  Tenants · menu master · reporting · SAF-T submission to AT · back-office    │
└───────────────▲──────────────────────────────────────────┬──────────────────┘
                │  outbox sync (HTTPS, idempotent)         │  pull config
┌───────────────┴──────────── RESTAURANT SITE ─────────────▼──────────────────┐
│  SITE AGENT  (.NET worker on a mini-PC or the main terminal)                │
│   • SQLite local store          • Fiscal signing + series numbering         │
│   • ESC/POS printer driver      • LAN REST + SignalR hub                    │
│   • Holds the RSA private key   • Outbox → cloud                            │
└───▲──────────────▲──────────────▲──────────────────────────────────────────┘
    │ LAN          │ LAN          │ LAN
 POS PWA        KDS PWA      (QR self-order → via cloud → agent)
```

**Why the Site Agent is worth its cost.** It is a .NET application — your strongest language — and it collapses three hard problems into one deployable unit: it custodies the fiscal key so documents can be signed offline; it drives thermal printers that a browser cannot reach; and it keeps multiple terminals in the same restaurant in sync over LAN even with the internet down. Without it you would need cloud-connected printers, cloud-only signing, and a POS that stops taking payments during an outage.

**Why offline fiscal signing is legal here.** Portuguese law permits multiple document series, and each series is registered with AT independently to obtain its série validation code. Assign each site (later, each terminal) its own series, and the Site Agent can issue gapless, correctly-chained documents with no connectivity. The cloud is a replica and the SAF-T submitter — it never re-numbers or re-signs. This single rule keeps exactly one signing authority per series and removes an entire class of reconciliation bugs.

### Modular monolith, not microservices

One deployable ASP.NET Core app, internally split into modules with enforced boundaries — each owns its own EF Core schema and exposes an internal contract, never another module's tables. Communication between modules goes through in-process integration events (MediatR) written to a transactional outbox. When a module needs to become its own service, the seam already exists: swap the in-process dispatcher for a message broker. For a solo developer this is the only responsible choice — microservices now would cost you the entire six months in operations work.

### Repository layout

```
/src
  /backend
    Brasa.Api/                 ASP.NET Core 10, Minimal APIs, /api/v1
    Brasa.Shared/              Money, Result, TenantContext, Outbox, Clock
    Brasa.Modules.Identity/    Users, roles, staff PINs, terminal pairing
    Brasa.Modules.Catalog/     Menu, modifiers, price lists, tax rules
    Brasa.Modules.Ordering/    Orders, tables, courses, splits, transfers
    Brasa.Modules.Fiscal/      IFiscalProvider, document lifecycle, audit
    Brasa.Modules.Payments/    Tenders, cash sessions, tips
    Brasa.Modules.Reporting/   Read models, X/Z reports, VAT summaries
    Brasa.Fiscal.Portugal/     ATCUD, RSA chain, QR, SAF-T PT, AT webservices
    Brasa.Fiscal.Mock/         Deterministic fake for dev and tests
  /agent
    Brasa.SiteAgent/           SQLite, ESC/POS, LAN hub, cloud sync
  /web
    pos/    kds/    admin/    order/    ui/    sdk/
/tests
/infra
```

`Fiscal.Portugal` and `Fiscal.Mock` both implement `IFiscalProvider`. This is what makes the mock-first workflow possible and what makes later country expansion an additive change rather than a rewrite.

### Technology choices

| Layer | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 (LTS) | Long support window for a product certified against a fixed baseline |
| API | ASP.NET Core Minimal APIs, vertical slices | Least ceremony per endpoint for one developer |
| Database | PostgreSQL + EF Core 10 (Npgsql) | Row-Level Security for tenant isolation; cheap to host in the EU |
| Realtime | SignalR | First-party .NET; drives POS ↔ KDS ↔ back-office |
| Jobs | Hangfire | Dashboard and retries out of the box — matters for AT submissions |
| Local store (agent) | SQLite + EF Core | Same ORM and model code as the cloud |
| Printing | ESC/POS over TCP/USB from the agent | Direct control, no printer-vendor cloud dependency |
| Web clients | React + TypeScript + Vite, PWA | Mature offline tooling (Dexie/IndexedDB), rich touch-UI ecosystem |
| Hosting | Hetzner + Docker Compose + Caddy, EU region | ~€20–40/month at MVP; GDPR data residency; clear path to managed services |

**On Blazor:** you are strongest in C#, so Blazor WASM is tempting. Do not use it here. A POS must boot in under two seconds on a cheap Android tablet and run reliably offline, and React's PWA/IndexedDB tooling and touch-UI component ecosystem are materially ahead for this specific workload. Your C# strength is spent where it counts — the API, the fiscal engine, and the Site Agent, which is the majority of the difficult code.

### Client applications

All four are React + TypeScript, sharing `web/ui` and a `web/sdk` client generated from the API's OpenAPI document.

- **pos** — Offline-first PWA. Dexie for local state, outbox for mutations. Talks to the Site Agent over LAN first, cloud as fallback.
- **kds** — Kitchen Display. Station routing, course firing, prep timers, bump. LAN-only, so it survives outages.
- **admin** — Back-office SPA. Menu, floor plan, staff, pricing, reports, tenant settings. Cloud-only.
- **order** — Public QR self-ordering. Guest scans a table QR, browses, orders. Reaches the cloud, which pushes to the Site Agent. Payment stays with staff at the table in MVP, which preserves the single-signing-authority rule.

---

## Fiscal engine (the crown jewel)

This is the part that must be exactly right. Build it against AT's test environment, and have a Portuguese accountant review the output before you submit Modelo 24.

**Components:**

- **Series registration** — Each `FiscalSeries` is communicated to AT via webservice, returning a série validation code. Persist it; it is an input to every ATCUD.
- **ATCUD** — `{validationCode}-{sequentialNumber}`, printed on every document.
- **Signature chain** — RSA private key, SHA-1 hash over `{InvoiceDate};{SystemEntryDate};{InvoiceNo};{GrossTotal};{PreviousHash}`, Base64-encoded. Store the full hash; print the 4 characters at positions 1, 11, 21, 31. Each document chains to the previous one **in its own series**.
- **QR code** — Pipe-delimited field string per AT's specification, rendered at minimum 30×30mm.
- **SAF-T (PT)** — XML export validated against AT's XSD, covering MasterFiles and SourceDocuments. Monthly invoicing submission by the 5th, automated via Hangfire with retry and alerting.
- **Document types** — `FS` fatura simplificada (the restaurant workhorse, permitted up to €1,000 to final consumers), `FT` fatura with NIF, `FR` fatura-recibo, `NC` nota de crédito. Critically, the pre-bill handed to a table before payment is a **documento não fiscal** and must be generated and labelled as such — never as an invoice.
- **Immutability** — Append-only audit log. No code path may update or delete an issued document; corrections happen only through credit notes. A chain-verification job re-walks each series and alarms on any break.

**Tax model.** Do not hardcode rates. Model `TaxRule` keyed by item × channel (dine-in / takeaway / delivery) × region (Continental / Madeira / Açores), each with an effective date range. As of 2026 the headline rates are 13% for meals and non-alcoholic drinks and 23% for alcoholic drinks, with regional variants — but rates change, takeaway treatment has shifted historically, and there is active political debate about the 13% band. **Have an accountant confirm current rules before launch;** the data model above absorbs any answer they give.

**Money.** Integer minor units everywhere. Never `double`, never floats. Currency-aware from the first commit.

---

## Scale decisions to make on day one

These are cheap now and brutally expensive to retrofit.

1. **Multi-tenancy in the schema from the first migration.** `tenant_id` on every table, EF Core global query filters, plus PostgreSQL Row-Level Security as defence-in-depth. Hierarchy: Organization → Site → Terminal.
2. **Idempotency keys on every mutating endpoint.** Required anyway for offline sync retries; also the precondition for any future distributed setup.
3. **Transactional outbox + integration events**, in-process today, broker-ready tomorrow.
4. **Versioned public API (`/api/v1`).** Treat the POS as merely the first client. Delivery-platform and accounting integrations arrive through the same door.
5. **Reporting read models separated from transactional tables**, so an owner running a year-end report can never add latency to a terminal taking an order.
6. **UTC storage, `Europe/Lisbon` rendering** — and note the Azores are a different timezone, which affects daily close and SAF-T boundaries.
7. **pt-PT primary with en fallback** from the first screen. Retrofitting i18n costs weeks.
8. **Per-tenant feature flags**, which is what later makes SMB / Pro / Enterprise tiering a configuration change.
9. **`IFiscalProvider` is the country seam.** Spain (Verifactu/TicketBAI), France (NF525) and Italy all become new implementations rather than forks.

---

## Roadmap

### Month 0 — Foundations (weeks 1–2)
Solution scaffolding, Docker Compose, CI. Tenancy plumbing with RLS. Identity, RBAC, staff PIN auth, terminal pairing. Core domain model and first migrations. API conventions: versioning, idempotency, ProblemDetails, OpenAPI → TS SDK generation.

> **Start company formation now, in parallel.** AT certification is granted to the producer, so you need a Portuguese entity with a NIF and a regularised tax situation *before* you can submit Modelo 24. Formation plus tax registration takes weeks and has no dependency on code. Starting this in Month 6 would idle you at the finish line.

### Month 1 — Catalog and back-office (weeks 3–6)
Menu: categories, items, modifier groups, price lists. `TaxRule` engine with channel and region dimensions. Sites, rooms, tables, drag-and-drop floor-plan editor. Back-office SPA with staff and role management.

### Month 2 — POS core (weeks 7–10)
POS PWA shell, Dexie local store, sync engine (outbox push + delta pull). Order lifecycle: open table, add lines, modifiers, courses, notes. Send-to-kitchen, table transfer, split and merge. Pre-bill as a documento não fiscal. Offline behaviour proven by deliberately killing the network mid-service.

### Month 3 — Site Agent, KDS, printing (weeks 11–14)
Site Agent: SQLite store, LAN REST + SignalR hub, pairing flow, cloud outbox sync. ESC/POS printing with station routing, retry, and printer-down fallback. KDS PWA: stations, course firing, bump, prep timers. Cash sessions — abertura and fecho de caixa, cash movements, blind counts.

### Month 4 — Fiscal engine (weeks 15–18) ⚠️ critical path
Series registration against AT's test environment. ATCUD, RSA signature chain, hash persistence and verification job. QR generation. Document types FS/FT/FR/NC plus non-fiscal. SAF-T PT export validated against the official XSD. Payments: cash, card (manually captured from a standalone TPA), split payments, tips. Immutable audit log. **Accountant review of real generated documents before proceeding.**

### Month 5 — Self-ordering, reporting, hardening (weeks 19–22)
QR self-ordering PWA end-to-end. Reports: X/Z, sales by item / category / hour / staff, VAT summary, SAF-T download. Soak testing, offline chaos testing, security review, backup and restore drill. Pilot restaurant onboarded in parallel-run mode alongside their existing certified system.

### Month 6 — Certification and launch (weeks 23–26)
Submit Modelo 24. AT's formal review is 30 days, suspended while conformity tests run — expect at least one remediation cycle, so submit at the *start* of this month. Meanwhile: tenant onboarding flow, Stripe subscription billing, documentation, support tooling. Go live once the certificate number is issued and printed on documents.

---

## Deferred (post-launch, in rough priority order)

Inventory and stock control · recipes and food costing · integrated TPA payments (SIBS, Unicre, SumUp, myPOS) · MB WAY and Multibanco references via Ifthenpay or Easypay · delivery-platform integrations (Uber Eats, Glovo, Bolt Food) · reservations · loyalty and CRM · staff scheduling and time clock · accounting exports (Primavera, Sage, PHC) · multi-location consolidated reporting · migration importers from WinRest and Zone Soft — which matter commercially, because your buyers already own one of those.

---

## Risks

| Risk | Mitigation |
|---|---|
| Certification slips past Month 6 | Entity formed in Month 0; build against AT's test environment from Month 4; submit at the start of Month 6, not the end |
| Six months solo proves too tight | Cut QR self-ordering first, then advanced reporting. Never cut the fiscal engine |
| No legal way to run a paid pilot pre-certificate | Run the pilot in parallel-run mode — real orders and kitchen flow through your system, fiscal documents still issued by the restaurant's existing certified software |
| Thermal printer model variance | Constrain MVP to a documented shortlist of ESC/POS models; test against real hardware, not emulators |
| Incumbents (WinRest, Zone Soft, Cegid Vendus) entrenched | Compete on offline reliability, modern UX and migration tooling — not on feature count |
| GDPR/RGPD exposure | EU-only hosting, records of processing, sub-processor DPAs, defined retention. Fiscal records retained 10 years |

---

## Verification

**Per module, continuously**
- Unit tests on domain logic — pricing, tax, splits, rounding — with the money type covered by property-based tests.
- Integration tests against a real PostgreSQL via Testcontainers. No in-memory provider; RLS behaviour must be exercised for real.
- Multi-tenant isolation test asserting that tenant A can never read tenant B's rows, run against every module.

**Fiscal engine (highest bar)**
- Golden-file tests: fixed inputs produce byte-identical signatures, ATCUDs and QR payloads.
- Chain-integrity test issuing 10,000 documents across interleaved series, then re-walking each chain and asserting no gap or break.
- SAF-T output validated against AT's official XSD in CI, so a schema regression fails the build.
- Round-trip against AT's **test environment** for series registration and submission.
- Independent review by a Portuguese accountant on real printed documents before submitting Modelo 24.

**Offline and sync (second-highest bar)**
- Chaos suite: kill the network mid-order, mid-payment, mid-print; assert no lost order, no duplicate document, no gap in the series.
- Idempotency test replaying every mutating request 3× and asserting a single effect.
- Two terminals editing the same table concurrently, asserting the ownership-and-transfer protocol holds.

**End-to-end, on real hardware**
Run the full service loop on an actual tablet with an actual thermal printer: seat a table → take an order with modifiers and courses → fire to KDS → print to kitchen and bar → add a round → split the bill → issue an FS with valid ATCUD and QR → verify the QR scans and decodes correctly → close the cash session → confirm the Z report and the SAF-T export reconcile to the cent.

**Load, before launch**
Simulate 50 sites × 5 terminals at dinner-service order rates; assert p95 API latency under 200ms and that reporting queries never touch the transactional path.

---

## Sources

- [Portugal's E-Invoicing Rules: Certified Software, ATCUD, QR Codes, and SAF-T — VATupdate](https://www.vatupdate.com/2026/05/29/portugals-e-invoicing-rules-certified-software-atcud-qr-codes-and-saf-t/)
- [ATCUD, SAF-T and QES in Portugal: Fiscalization rules explained (2026) — fiskaly](https://www.fiskaly.com/blog/fiscalization-atcud-qes-in-portugal)
- [Pedir certificação de programa de faturação — gov.pt](https://www.gov.pt/servicos/programa-de-faturacao-certificacao)
- [Portaria n.º 363/2010 (consolidada) — Diário da República](https://diariodarepublica.pt/dr/legislacao-consolidada/portaria/2010-119668497)
- [Portaria n.º 340/2013 — Diário da República](https://diariodarepublica.pt/dr/detalhe/portaria/340-2013-503842)
- [IVA na Restauração 2026: Taxas 13%, 23% e 6%](https://calculariva.pt/setores/restauracao/)
- [Planos e Preços — InvoiceXpress](https://invoicexpress.com/planos-precos-v2)
- [Software para Restaurante em Portugal: Guia 2026 — Bitte](https://joinbitte.com/blog/software-restaurante-portugal-guia)
