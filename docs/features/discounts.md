# Discounts

> **Status:** ✅ built
> **Module:** Ordering
> **Roadmap:** I2 (pulled forward)

## What it is

A manager can knock a percentage or a fixed euro amount off a single line
("half off — this dish came out cold") or off the whole order ("10% for a
regular"). The two compose: an order-level discount applies on top of the
subtotal that's already left after any line discounts, not instead of them.

## Why it works this way

**No manager-authorisation gate yet.** Real POS discounting is normally
gated behind a manager PIN to control shrinkage. That gate is IDN-11, and
IDN doesn't exist yet — this feature ships the underlying mechanism ahead
of the trigger, the same shape already established for CAT-13 (86-ing) and
CAT-19 (repricing): build the real capability now, wire the authorisation
check in later without touching this code's shape.

**A fixed discount that would exceed the total it's applied to is
rejected, not clamped.** Silently capping "€50 off" to "€12 off" on a
€12 line hides a typo from the person who made it. Rejecting it outright
(`order.invalid_discount`) surfaces the mistake immediately.

**A discount never touches `OrderLine.UnitPrice`.** That price is a
snapshot taken when the line was rung up and must stay exactly what the
guest was charged at that moment — the same rule that makes a past
order's total permanent when the menu item's price changes later. A
discount is a separate reduction layered on top, not a rewrite of the
snapshot.

**A discount is rendered on the fiscal document as its own negative line,**
not as an adjustment to a per-unit price. Folding a discount into a
per-unit price for a multi-quantity line would either lose a cent or need
`Money.Allocate`'s remainder logic for no reason (`Money` deliberately has
no division operator — see [money.md](../architecture/money.md)). An
order-level discount is prorated across lines by `Money.Allocate` — the
same proportional-distribution tool the by-cover split already uses —
which is what guarantees `order.Total` and the issued document's
`GrossTotal` reconcile to the cent by construction, not by convention.

## Behaviour

1. Staff sets a line discount: `PUT /orders/{id}/lines/{lineId}/discount`
   with `{ "type": "Percentage", "value": 10 }` (or `"FixedAmount"` with a
   euro value). The line's `discountAmount` and `lineTotal` update
   immediately in the response.
2. Staff sets an order-level discount the same way, against
   `PUT /orders/{id}/discount`. It's computed against the *post-line-discount*
   subtotal, so it stacks rather than overlapping.
3. Clearing either is the same call with both fields `null`.
4. The pre-bill (`GET /orders/{id}/pre-bill`) and the eventual close both
   reflect the discounted total — a guest asking to see the bill sees the
   same number the invoice will show.
5. Closing the order (`POST /orders/{id}/close`) issues a fiscal document
   whose `GrossTotal` equals the discounted `order.Total` exactly, with
   each discount appearing as its own `"Desconto: <item>"` line.

## Offline behaviour

Not applicable — this is a cloud API endpoint with no offline path today.
Every write here goes through the same `Idempotency-Key` contract as every
other mutation (hard rule 7); there's nothing discount-specific about it.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Unrecognised `type` string | Rejected, no state changed | `400 order.invalid_discount` |
| Only one of `type`/`value` given | Rejected | `400 order.invalid_discount` — "needs both... or neither" |
| Percentage outside (0, 100] | Rejected | `400 order.invalid_discount` |
| Fixed amount ≤ 0, or exceeds the total it's applied to | Rejected, never clamped | `400 order.invalid_discount` |
| Discount set on an unknown line | Rejected | `404 order.line_not_found` |
| Discount attempted on a closed/merged order | Rejected | `409 order.not_open` |

## Data

`OrderLine.DiscountKind`/`DiscountValue` (line-level) and
`Order.DiscountKind`/`DiscountValue` (order-level) — both owned by
Ordering, both nullable, both stored as a string-converted enum + a
`numeric(10,2)` value. Named `DiscountKind`, not `DiscountType`, so the
property doesn't shadow the `DiscountType` enum's own name inside the
class that declares both (see the trap in
[docs/ai/README.md](../ai/README.md) if this comes up again elsewhere).
Nothing is persisted for the *computed* discount amount — `DiscountAmount`
and `LineTotal`/`Total` are always derived, same as everywhere else in
this aggregate.

## API

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/orders/{orderId}/lines/{lineId}/discount` | Set or clear a line's discount |
| `PUT` | `/orders/{orderId}/discount` | Set or clear the order's discount |

Both take `Idempotency-Key` like every other mutation. Both return the
full `OrderDto`, which now carries `discountType`/`discountValue`/
`discountAmount` at both the order and line level.

## Integration events

None. Modules don't publish integration events yet at all (FND-11 is
still `⬜`) — see [module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

Yes — directly changes the gross total a fiscal document charges.
`OrderEndpoints.BuildFiscalLines` is the single place that turns an
order's lines (and any discounts) into the `FiscalDocumentLine`s handed to
`IFiscalProvider`, shared between the pre-bill preview and the real close
so both always agree. Not certification-relevant work itself (no new
document type, no change to the signature chain), but the reconciliation
invariant it depends on (`order.Total == document.GrossTotal` to the
cent) is exactly the kind of thing an accountant reviewing sample
documents would check.

## Permissions

None enforced yet. This is the gap IDN-11 (manager-authorisation flow for
voids and discounts) is meant to close — see "Why it works this way"
above. Until then, anything that can call the API can discount anything.

## Testing

`discounts.spec.ts` — API-level only, no `pos`/`admin` UI exists yet to
drive through a browser. Covers: line discount reducing only that line;
order discount reducing the post-line-discount subtotal further; clearing
restoring the original total; every rejection path; and the two fiscally
load-bearing checks — the pre-bill's VAT breakdown still sums to
`order.Total`, and `document.GrossTotal` still equals `order.Total` to the
cent once the order is actually closed with a discount applied.

## Open questions

- `SplitByItem`'s by-item preview doesn't yet fold in a discount (it
  computes portions from a line's raw unit price, not `LineTotal`) —
  `SplitEvenly`/`SplitByCover` don't have this gap, since both inherit a
  discount automatically through `Order.Total`. Narrow edge case; not
  fixed yet.
- No reason/note field on a discount. Real restaurants often want one for
  later shrinkage analysis (DIF-13) — deliberately left out for now rather
  than guessed at ahead of that need.
- Whether a discount should require a reason or manager PIN before
  IDN-11 exists is a product decision, not an engineering one.
