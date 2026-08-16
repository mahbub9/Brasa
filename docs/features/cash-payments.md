# Cash payments — a tender recorded against an order's remaining balance

> **Status:** ✅ done — cash only, partial tenders supported. Card/MB WAY
> (PAY-03) and split-across-methods (PAY-04) and cash-session/blind-count
> reconciliation (PAY-08/10/11) are all deliberately out of scope.
> **Module:** Payments (new module this task), composed with Ordering at the
> API layer
> **Roadmap:** I3/I4 (`PAY-01`, `PAY-02`, `PAY-05`)

## What it is

Real service closes an order (issues the fiscal document) without ever
recording how the guest actually paid — nothing in the codebase before this
tracked a tender or computed change. `Payment` is a new record, in a new
`Brasa.Modules.Payments` module, that captures one cash tender against one
order's remaining balance: what was still owed, what was handed over, and
the change owed back. `POST /orders/{orderId}/payments` records it;
`GET /orders/{orderId}/payments` lists every payment recorded against an
order. An order can be settled in one tender or several — a tender smaller
than what's owed is a valid partial payment, and the balance tracks across
however many it takes to reach zero.

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

**`AmountDue` is a remaining balance, not always the order's full total
(PAY-05).** `RecordPaymentAsync` sums every prior payment's own
`AmountApplied` for the order and subtracts that from `order.Total` before
constructing the new `Payment` — so a second or third tender only needs to
cover what's left, never the whole order again. `Payment.AmountApplied` is
the smaller of what was tendered and what was due, `Change` is whatever's
left over (`AmountTendered - AmountApplied`, always zero unless this tender
overpaid), and `RemainingBalance` is `AmountDue - AmountApplied` — zero once
the order is fully settled. A tender for less than what's owed is not an
error: it's a partial payment, recorded exactly as-is, with `RemainingBalance`
staying positive. Once the balance reaches zero, a further payment is
rejected with `payment.already_settled` rather than silently accepted —
closing the "nothing stops a double payment" gap this page originally left
open as a known trade-off.

**Splitting one tender across several *methods* is still a separate task
(PAY-04).** Every `Payment` row here is exactly one method — PAY-05 lets a
guest pay in several cash tenders, not in one payment split across cash and
card at once. `PaymentMethod` has exactly one case (`Cash`) today regardless.

**The server reads the amount due — the client never sends one.** The
request body carries only `method` and `amountTendered`; `RecordPaymentAsync`
loads the order and every prior payment itself and computes the remaining
balance server-side. A client-sent due amount would let a stale or
manipulated screen record a payment against the wrong balance.

**`Money.ToString()`, not culture-formatted, in error messages.** An earlier
draft embedded amounts via `Money.Format(CultureInfo.InvariantCulture)` in
the (since-removed) `payment.insufficient_tender` message and rendered a bare
"¤" currency symbol with no code — worse than the plain invariant diagnostic
form (`"300 EUR"`) for an API error string nobody localizes. The convention
carried forward to every message this endpoint builds.

## Behaviour

1. Staff closes an order (or not — see above) and reaches the receipt
   screen, which now always shows a "Cash payment" panel with the amount
   currently due.
2. Staff enters what the guest handed over and submits.
3. The API computes the order's own remaining balance server-side, validates
   the method and the amount, and persists a `Payment`, returning the
   computed `change` and `remainingBalance`.
4. If `remainingBalance` is still positive, the receipt screen's form stays
   open — showing the updated balance and a running list of tenders already
   recorded — so staff can record another one.
5. Once a tender brings the balance to zero, the form is replaced with a
   confirmation showing the change due from whichever tender settled it.
   There is no edit or void path for a recorded payment yet.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today, same as every
other mutating endpoint in this codebase before the eventual SYN work.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Tendered amount is zero or negative | Rejected before the order is even loaded | `400 payment.invalid_amount_tendered` |
| Method is anything other than `Cash` | Rejected — only `PaymentMethod.Cash` exists today | `400 payment.unsupported_method` |
| Order id doesn't exist | No payment written | `404 order.not_found` (both on record and on list) |
| A tender is smaller than the remaining balance | Not an error — recorded as a partial payment, `remainingBalance` stays positive | `201` with `change: 0` and a positive `remainingBalance` |
| A payment is recorded against an order whose balance is already zero | Rejected before any row is written | `400 payment.already_settled` |
| Two terminals record a payment for the same order at once | Both succeed independently — `Payment` carries no concurrency token, and each reads "prior payments" from its own snapshot in time | Both tenders persist; if both read the balance before either wrote, the order can be over-settled (e.g. two terminals each independently see the full balance still owed). A known, accepted gap — see Open questions |

## Data

New `payments` schema, one table (`payments.payments`): `Id`, `OrderId`
(indexed, unindexed foreign reference — see module-boundaries note above),
`Method` (int enum), `AmountDue`/`AmountTendered` (mapped via `MapMoney`,
the same convention every other `Money` column in this codebase uses),
`PaidAtUtc`. `AmountApplied`/`Change`/`RemainingBalance` are all computed
properties, not columns — `PaymentConfiguration` ignores all three, the same
"never store a derived total" rule `Order.Total` follows. RLS is enabled the
standard way (`EnableFor`/`EnableSystemReadFor` in the migration's `Up()`,
hand-added since the EF Core scaffolder never emits these calls — see
[multi-tenancy.md](../architecture/multi-tenancy.md)).

## API

- `POST /orders/{orderId}/payments` — body `{ method, amountTendered }`,
  returns `201` with a `PaymentDto` (`amountDue`, `amountTendered`,
  `amountApplied`, `change`, `remainingBalance`, all as `MoneyDto`).
- `GET /orders/{orderId}/payments` — returns every payment recorded against
  the order, oldest first (sorted client-side in the endpoint — SQLite,
  [ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md),
  can't translate `ORDER BY` over `DateTimeOffset`, the same limitation
  `TaxRuleEndpoints.GetTaxRulesAsync` already works around).

Both documented in `docs/openapi/v1.json` and typed in
`src/web/sdk/src/schema.ts` (regenerated when `PaymentDto` grew its two new
fields for PAY-05).

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
(method parsing, amount checks, balance computation, order lookup) — no
dedicated `Brasa.Api.IntegrationTests` class yet; the full backend suite
(101 tests) and `verify.ps1` (build, tests, OpenAPI drift, breaking-change
check, vulnerable-package scan) all pass with the module wired in.

**`src/web/e2e/tests/payments.spec.ts`** — 7 tests: a tender covering the
total with correct change; a tender smaller than the total recorded as a
valid partial payment, tracked correctly across a settling second tender
that itself overpays (proving `AmountApplied`/`Change`/`RemainingBalance`
compose correctly, not just in isolation), and a further tender against the
now-settled order rejected with `payment.already_settled`; zero/negative
amounts, an unsupported method, and an unknown order each rejected with the
right code; listing payments across a partial-then-settling pair (empty,
then two rows, then 404 for an unknown order); recording a payment *after*
close to prove close isn't a precondition; and two real-browser UI tests —
one full tender, one two-tender partial-then-settle flow through the
receipt screen's cash-payment panel, including its running balance and
payment-history list.

## Open questions

- **Two terminals racing the same order's balance can over-settle it.**
  `RecordPaymentAsync` reads "prior payments" and writes the new one in two
  separate steps with no concurrency token guarding the order's aggregate
  balance — unlike `Order` itself (ORD-21's `xmin` token), a `Payment` row's
  own save can never conflict, because each terminal is inserting a *new*
  row, not updating a shared one. Two terminals that both read the balance
  as "still owed" before either write lands can each successfully record a
  payment, together exceeding the total. Low real-world likelihood (cash
  payments happen at a till, rarely two-at-once against the same order) but
  a real, unclosed gap — revisit if PAY-08's cash session work ever needs a
  stronger guarantee than "extremely unlikely in practice."
- **No UI or API path to void or correct a recorded payment.** A mis-entered
  tender amount has no correction path today beyond direct database access —
  same class of gap as `Order`'s own void-a-line design solves for order
  lines, not yet solved here.
- **`PaymentMethod` has exactly one case (`Cash`).** Adding `Card`/`MBWay`
  (PAY-03) is additive to the enum and the endpoint's validation, not a
  redesign — but nothing about routing a card payment to an actual payment
  processor exists yet. Splitting one settlement across several methods at
  once (PAY-04) is a separate, larger change: today's model only lets a
  guest pay in several tenders of the *same* method.
