# Architecture

## The problem this shape solves

Three requirements collide in a browser-based restaurant POS:

1. **A restaurant's internet will fail**, and service cannot stop when it does.
   A waiter must still be able to open a table, take an order, fire it to the
   kitchen, and take payment.
2. **Portuguese fiscal documents require a chained RSA signature** over a gapless
   per-series sequence. So signing must be possible without connectivity — but a
   private key cannot live safely in a browser.
3. **A PWA cannot open raw TCP sockets**, so it cannot drive ESC/POS kitchen
   printers or a cash drawer.

All three are solved by one component: a process that runs *inside* the
restaurant. That is the Site Agent, and its existence is the single most
consequential decision in this system.

## Three tiers

```
┌─────────────────────── CLOUD (EU region) ───────────────────────────────────┐
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

### Who owns what

| Concern | Authority | Why |
|---|---|---|
| Menu, prices, staff, floor plan | **Cloud** | Edited in the back-office, pulled by sites |
| Open orders | **Site Agent** | Must work with no internet |
| Fiscal document numbering + signature | **Site Agent** | Offline signing; one authority per series |
| SAF-T generation and submission to AT | **Cloud** | Monthly, needs the full picture, needs internet |
| Reporting | **Cloud** | Must never add latency to a terminal taking an order |

### Why offline fiscal signing is legal

Portuguese law permits **multiple document series**, each registered with AT
independently to obtain its own série validation code. Assign each site (later,
each terminal) its own series, and the Site Agent can issue gapless, correctly
chained documents with no connectivity at all.

The cloud is a **replica and the submitter**. It never re-numbers and never
re-signs. This gives exactly one signing authority per series and removes an
entire class of reconciliation bugs. See
[decisions/0003-site-agent.md](decisions/0003-site-agent.md).

## Clients, and the API that serves them

Android and iOS apps follow shortly after the web launch — staff handheld
ordering, an owner dashboard, a customer app, and a native kitchen display —
on an as-yet-undecided stack.

**One versioned REST API serves all of them.** There is no
backend-for-frontend, no cookie authentication, and no web-only assumption
anywhere in the contract, so shipping those apps requires no backend change.
Two surfaces, never one:

| Surface | Serves | Auth |
|---|---|---|
| `/api/v1` | Staff, managers, terminals | Terminal pairing + staff PIN, or user account |
| `/api/public/v1` | Customers | Separate consumer identity realm |

The rules are in **[api-contract.md](api-contract.md)**; read it before adding
any endpoint. The reasoning is in
[ADR 0007](decisions/0007-client-agnostic-api.md) and
[ADR 0008](decisions/0008-token-auth-no-cookies.md).

## Modular monolith

One deployable API, internally split into modules with enforced boundaries.
Modules do not reference each other and never read each other's tables; they
communicate through integration events written to a transactional outbox.

When a module needs to become its own service, the seam already exists — swap the
in-process dispatcher for a message broker. Publishers and handlers do not change.

See [module-boundaries.md](module-boundaries.md) and
[decisions/0001-modular-monolith.md](decisions/0001-modular-monolith.md).

## The shared kernel

`Brasa.Shared` is depended on by every module and depends on none. It is
deliberately small:

| Area | Type | Notes |
|---|---|---|
| Money | `Money`, `CurrencyCode` | Integer minor units, allocation-based splitting — [money.md](money.md) |
| Outcomes | `Result`, `Result<T>`, `Error` | Expected failures are values, not exceptions |
| Tenancy | `ITenantContext`, `TenantContext` | Resolve-once-per-scope — [multi-tenancy.md](multi-tenancy.md) |
| Time | `IClock`, `PortugueseRegion` | Never call `DateTime.UtcNow` directly |
| Persistence | `Entity`, `ITenantOwned`, `IAuditable` | UUIDv7 ids, generated offline |
| Messaging | `IIntegrationEvent`, `OutboxMessage` | The only cross-module channel |

## Decisions baked in from day one

These are cheap now and expensive to retrofit:

1. **Multi-tenancy in the schema from the first migration** — `tenant_id`
   everywhere, EF global query filters, plus PostgreSQL RLS as the real defence.
2. **Idempotency keys on every mutating endpoint** — needed for offline sync
   retries regardless, and the precondition for any distributed future.
3. **Transactional outbox** — in-process today, broker-ready tomorrow.
4. **Versioned public API (`/api/v1`)** — the POS is merely the first client.
5. **Reporting read models separated from transactional tables.**
6. **UTC storage, regional rendering** — the Azores are an hour behind the
   mainland, which affects daily close and SAF-T period boundaries.
7. **pt-PT primary with en fallback** from the first screen.
8. **`IFiscalProvider` is the country seam** — Spain, France and Italy become new
   implementations rather than forks.
