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

Not everything needs copying, though — only business facts that must survive
the referenced row changing later. `Order.TableId` is a plain `Guid`
reference to a Floor `Table`, resolved fresh by the API layer on every call,
the same way `OrderLine.MenuItemId` is: Ordering stores the id, never a
navigation property, and never queries `floor.tables` itself. The difference
from the price/name case is that a table reference has no "value at the
time" to preserve — `TableLabel` is what gets snapshotted, for exactly the
receipt reason above, while `TableId` stays a live-ish pointer an endpoint
can resolve when it needs current state (is this table still occupied?).

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

**"Calling Fiscal" means `IFiscalProvider`, not any type that happens to live
in that module.** `OrderEndpoints.GetPreBillAsync` (ORD-18/19) constructs
`FiscalDocumentLine` — pure gross→net/VAT arithmetic, no side effects — to
compute a pre-bill preview, but never calls `IFiscalProvider`. That is the
whole point: a pre-bill is a *documento não fiscal*, and calling the provider
would issue and number a real document for a table that only asked to see
its total so far. Reusing the calculation avoids duplicating VAT-derivation
logic; not calling the provider is what keeps the preview non-fiscal.

**This means composing endpoints save more than one `DbContext`, and that is
not one transaction.** `OrderEndpoints.OpenOrderAsync` saves Ordering, then
Floor; `CloseOrderAsync` saves Ordering, then (best-effort) Floor. Order the
saves so that if the second one fails, what's left is the more recoverable
inconsistency — see the comments at each call site for the specific
reasoning. Real cross-module atomicity is the outbox (rule 3), which is
async by design; a synchronous composing endpoint that needs two writes to
agree exactly, always, doesn't have a solution here yet. That is scoped work
for I5+, not an oversight to "fix" by reaching for a distributed transaction.

## The modules

| Module | Owns |
|---|---|
| `Identity` | Users, roles, staff PINs, terminal pairing |
| `Catalog` | Menu, categories, modifiers, price lists, tax rules |
| `Ordering` | Orders, courses, splits, transfers |
| `Floor` | Rooms, tables, table state (free / occupied / bill requested / dirty) |
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
