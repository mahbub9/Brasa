# Module boundaries

The system is a [modular monolith](decisions/0001-modular-monolith.md). The
boundaries are what make it possible to extract a service later without a
rewrite — and they only hold if they are respected consistently.

## The rules

### 1. Modules do not reference each other

`Brasa.Modules.Ordering` must not have a `ProjectReference` to
`Brasa.Modules.Catalog`. Every module references
`Brasa.Shared` and nothing else in the solution.

This is enforced by the module `.csproj` template — if you find yourself adding a
module-to-module reference, the design is telling you something.

### 2. Modules do not read each other's tables

Each module owns a PostgreSQL schema. No joins across schemas, no `DbSet` for
another module's entity.

When Ordering needs a menu item's name and price, it **copies them onto the order
line at the time the line is created**. That is not denormalisation for
performance — it is correctness. A receipt must show what the item cost *when it
was sold*, not what it costs today. The same applies to VAT rates.

### 3. Cross-module communication is integration events

The only sanctioned channel is
[`IIntegrationEvent`](https://github.com/mahbub9/Brasa/blob/main/src/backend/Brasa.Shared/Messaging/IntegrationEvent.cs),
written to the outbox in the same transaction as the state change that produced
it.

```
Ordering  ──OrderClosed──▶  outbox  ──▶  dispatcher  ──▶  Fiscal, Reporting
```

### 4. Handlers must be idempotent

The outbox guarantees **at-least-once** delivery. A handler will see the same
`EventId` twice after a retry or a crash between dispatch and acknowledgement.
Deduplicate on `EventId`.

### 5. The API layer composes; modules do not call each other

An endpoint may call Ordering and then Fiscal. Ordering may not call Fiscal
directly.

## The modules

| Module | Owns |
|---|---|
| `Identity` | Users, roles, staff PINs, terminal pairing |
| `Catalog` | Menu, categories, modifiers, price lists, tax rules |
| `Ordering` | Orders, tables, courses, splits, transfers |
| `Fiscal` | `IFiscalProvider`, document lifecycle, series, audit |
| `Payments` | Tenders, cash sessions, tips |
| `Reporting` | Read models, X/Z reports, VAT summaries |

## Why the discipline is worth it

The extraction test: *could this module be given its own database and process
without changing its callers?*

If yes, the boundary is real, and scaling later is a deployment change. If no —
if something reaches across and joins — extraction becomes a rewrite, which for a
solo developer means it never happens.

Reporting is the module most likely to be extracted first, because its load
profile diverges most sharply from the transactional path.
