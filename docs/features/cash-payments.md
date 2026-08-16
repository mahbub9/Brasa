# Cash payments — a tender recorded against an order, with change

> **Status:** ✅ done — cash only. Card/MB WAY (PAY-03), split/partial tender
> (PAY-04/05), and cash-session/blind-count reconciliation (PAY-08/10/11) are
> all deliberately out of scope.
> **Module:** Payments (new module this task), composed with Ordering at the
> API layer
> **Roadmap:** I3/I4 (`PAY-01`, `PAY-02`)

## What it is

Real service closes an order (issues the fiscal document) without ever
recording how the guest actually paid — nothing in the codebase before this
tracked a tender or computed change. `Payment` is a new record, in a new
`Brasa.Modules.Payments` module, that captures one cash tender against one
order: what was due, what was handed over, and the change owed back.
`POST /orders/{orderId}/payments` records it; `GET /orders/{orderId}/payments`
lists every payment recorded against an order.

## Why it works this way

**A new module, not a field on `Order`.** Payments is a distinct future
concern (methods, sessions, reconciliation, refunds) that has nothing to do
with how an order's lines and totals are computed — the same reasoning that
already keeps Ordering, Catalog, Floor and Fiscal apart. `Payment.OrderId` is
a plain opaque `Guid` reference, the same convention `Order.TableId` already
uses for a Floor `Table`: Payments never queries Ordering directly (see
[module-boundaries.md](../architecture/module-boundaries.md) rule 4).
`PaymentEndpoints` composes `PaymentsDbContext` and `OrderingDbContext` at the
API layer instead — the same shape `PriceListEndpoints` already uses for
Catalog+Identity.

**Purely additive — does not gate `Order.Close()`.** Real service and a
recorded payment both close over the same "guest paid" moment in practice,
but wiring a payment requirement into the already-proven close path would
touch the dozens of existing E2E specs that close an order without ever
recording one. This is the same "mechanism before the trigger" shape this
codebase already uses elsewhere (`TaxRule` not wired into `AddLine`, price
lists not resolved through it, DAT-07's `brasa_system` role with no real
consumer yet). This ships the record; requiring one before close — or
blocking close without one — is a deliberately separate, later decision.
A payment can be recorded before or after `Close()`; the endpoint doesn't
care which, and `payments.spec.ts`'s own test proves the "after" case
explicitly.

**Full payment only — no partial tender.** `Payment`'s constructor throws if
`amountTendered < amountDue`; there is no way to persist a partial payment.
Splitting a bill across several payments or several methods is PAY-04/05,
not this — `SplitByItemResponse` (ORD-15-ish) already computes *how much*
each group owes, but nothing today records that a specific group actually
paid it.

**The server reads the amount due — the client never sends one.** The
request body carries only `method` and `amountTendered`; `RecordPaymentAsync`
loads the order itself and reads `order.Total` as `amountDue`. A client-sent
due amount would let a stale or manipulated screen record a payment against
the wrong total.

**`Money.ToString()`, not culture-formatted, in the error message.** The
`payment.insufficient_tender` message embeds the two amounts via `Money`'s
plain invariant form (`"300 EUR"`), not `Money.Format(CultureInfo)` — an
earlier draft used the latter and rendered a bare "¤" currency symbol with no
code, which is worse than the plain diagnostic form for an API error string
nobody localizes.

## Behaviour

1. Staff closes an order (or not — see above) and reaches the receipt
   screen, which now always shows a "Cash payment" panel with the amount
   due.
2. Staff enters what the guest handed over and submits.
3. The API re-reads the order's own total server-side, validates the method
   and the amount, and — if the tender covers the total — persists a
   `Payment` and returns it, including the computed `change`.
4. The receipt screen replaces the form with a confirmation showing the
   change due. There is no edit or void path for a recorded payment yet.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today, same as every
other mutating endpoint in this codebase before the eventual SYN work.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Tendered amount is less than the order's total | Rejected before any row is written | `400 payment.insufficient_tender` |
| Tendered amount is zero or negative | Rejected before the order is even loaded | `400 payment.invalid_amount_tendered` |
| Method is anything other than `Cash` | Rejected — only `PaymentMethod.Cash` exists today | `400 payment.unsupported_method` |
| Order id doesn't exist | No payment written | `404 order.not_found` (both on record and on list) |
| Two terminals record a payment for the same order at once | Both succeed independently — `Payment` carries no concurrency token and there is no "already paid" check | Two `Payment` rows persist; nothing here stops a double-tender being recorded, since there's no policy yet for what "already paid" even means without PAY-04/05's split model |

That last row is a known, accepted gap for this increment — see Open
questions.

## Data

New `payments` schema, one table (`payments.payments`): `Id`, `OrderId`
(indexed, unindexed foreign reference — see module-boundaries note above),
`Method` (int enum), `AmountDue`/`AmountTendered` (mapped via
`MapMoney`, the same convention every other `Money` column in this codebase
uses), `PaidAtUtc`. `Change` is a computed property (`AmountTendered -
AmountDue`), not a column — `PaymentConfiguration` ignores it, the same
pattern `Order.Total` uses. RLS is enabled the standard way
(`EnableFor`/`EnableSystemReadFor` in the migration's `Up()`, hand-added
since the EF Core scaffolder never emits these calls — see
[multi-tenancy.md](../architecture/multi-tenancy.md)).

## API

- `POST /orders/{orderId}/payments` — body `{ method, amountTendered }`,
  returns `201` with a `PaymentDto` (`amountDue`, `amountTendered`, `change`
  all as `MoneyDto`).
- `GET /orders/{orderId}/payments` — returns every payment recorded against
  the order, oldest first (sorted client-side in the endpoint — SQLite,
  [ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md),
  can't translate `ORDER BY` over `DateTimeOffset`, the same limitation
  `TaxRuleEndpoints.GetTaxRulesAsync` already works around).

Both documented in `docs/openapi/v1.json` (regenerated as part of this
task) and typed in `src/web/sdk/src/schema.ts`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None directly — the fiscal document is issued by `CloseOrderAsync`
independently of whether or when a payment is recorded. A `Payment` is not a
fiscal document and is never treated as one.

## Permissions

None — any authenticated terminal can record a payment against any order,
same as every other ordering endpoint today. Cash-handling accountability
(who tendered, session/shift boundaries) is PAY-08/10/11, not this.

## Testing

**Backend:** covered indirectly through the endpoint's own validation logic
(method parsing, amount checks, order lookup) — no dedicated
`Brasa.Api.IntegrationTests` class yet; the full backend suite (101 tests)
and `verify.ps1` (build, tests, OpenAPI drift, breaking-change check,
vulnerable-package scan) all pass with the new module wired in.

**`src/web/e2e/tests/payments.spec.ts`** — 6 tests: a tender covering the
total with correct change; a tender below the total rejected with
`payment.insufficient_tender`; zero/negative amounts, an unsupported method,
and an unknown order each rejected with the right code; listing payments
(empty, then populated, then 404 for an unknown order); recording a payment
*after* close to prove close isn't a precondition; and a full real-browser
UI test through the receipt screen's new cash-payment panel. All pass
alongside the full 194-spec suite (the one incidental failure seen in a full
run, `transfer-table.spec.ts`'s UI test, is the pre-existing QA-02
table-pool-exhaustion flake — confirmed clean in isolation, unrelated to this
change).

## Open questions

- **Nothing stops a second payment being recorded against an already-settled
  order.** There's no "order is paid" state anywhere yet — `GET
  /orders/{id}/payments` lets a caller check what's already been recorded,
  but the record endpoint doesn't consult it. Revisit once PAY-04/05 (split
  tender) defines what "fully paid" actually means for an order that can
  have several payments by design.
- **No UI or API path to void or correct a recorded payment.** A mis-entered
  tender amount has no correction path today beyond direct database access —
  same class of gap as `Order`'s own void-a-line design solves for order
  lines, not yet solved here.
- **`PaymentMethod` has exactly one case (`Cash`).** Adding `Card`/`MBWay`
  (PAY-03) is additive to the enum and the endpoint's validation, not a
  redesign — but nothing about routing a card payment to an actual payment
  processor exists yet.
