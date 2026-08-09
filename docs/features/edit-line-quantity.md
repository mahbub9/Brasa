# Edit a line's quantity

> **Status:** ✅ built
> **Module:** Ordering
> **Roadmap:** I2

## What it is

Staff can change how many of an item on an already-open order were
ordered — a guest asks for one more, or the waiter over-rang before
anything reached the kitchen — without voiding the line and ringing up a
fresh one.

## Why it works this way

**This is not how a wrong or unwanted line is undone.** That distinction
matters: [Void a line](void-a-line.md) (ORD-10) exists specifically for
cancelling a line, requires a reason, and freezes the line's own
`Quantity` as an audit record of what was actually rung up.
`SetLineQuantity` rejects a voided line outright (`order.line_voided`)
rather than letting a quantity edit quietly reopen what void just closed
— editing around a void would defeat the entire point of the audit trail
it exists to preserve.

**"Remove a line" stays unbuilt as its own endpoint.** This feature's own
backlog row (ORD-03) names "add / remove / edit" as its scope. Add and
edit are both built; a bare delete deliberately is not, because void
already is the sanctioned way to take a line off the bill, and it comes
with a reason and an audit trail a plain delete would lose. Building a
second, weaker path to the same outcome would just give staff a way
around the first one.

**Recomputing the total costs nothing extra.** `OrderLine.LineTotal` was
already derived from `Quantity` (via `GrossBeforeDiscount`), not stored
independently, so changing `Quantity` recomputes it for free. A
percentage discount scales proportionally with the new gross
automatically; a fixed discount stays fixed but re-clamps via
`DiscountAmount`'s existing "never exceed the line's own gross" clamp if
shrinking the quantity would otherwise make the discount bigger than the
line itself.

## Behaviour

1. Staff changes a line's quantity: `PUT /orders/{id}/lines/{lineId}/quantity`
   with `{ "quantity": 3 }`.
2. The response shows the line with the new `quantity`, and `lineTotal`
   recomputed from it (net of any discount already on the line).
3. The order's `total` reflects the change immediately — no separate step.
4. `pos` shows a +/− stepper next to each line; "−" is disabled at
   quantity 1 (dropping to zero is what void is for, not an edit).

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Quantity below 1 | Rejected, nothing changed | `400 order.invalid_quantity` |
| Unknown line id | Rejected | `404 order.line_not_found` |
| Line already voided | Rejected — quantity stays frozen for audit | `409 order.line_voided` |
| Edit attempted on a closed/merged order | Rejected | `409 order.not_open` |

## Data

No new column — `OrderLine.Quantity` already existed (ORD-01). This
feature is a new *mutator* (`Order.SetLineQuantity` → `OrderLine.SetQuantity`),
not new storage.

## API

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/orders/{orderId}/lines/{lineId}/quantity` | Change how many of a line's item were ordered |

Takes `Idempotency-Key` like every other mutation. Returns the full
`OrderDto`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

Indirect only. `LineTotal` (already fiscal-relevant via `BuildFiscalLines`)
recomputes from the new quantity, so a closed order always reflects
whatever quantity was true at close time — no different from any other
line mutation made before close. No new document type, no change to how
`IFiscalProvider` is called.

## Permissions

None enforced yet. Unlike voiding or discounting, this one arguably
needs no manager gate at all — correcting a quantity mistake before a
dish is delivered is normal waitstaff judgement, not a shrinkage vector
in the way an unrestricted void or discount is. Revisit if that
assumption turns out wrong once real service data exists.

## Testing

`order-line-quantity.spec.ts` — increasing/decreasing recomputes the
line and order totals to the cent; a percentage discount scales with
the new quantity and a fixed one re-clamps correctly if it would now
exceed a smaller line's gross; a quantity below 1, an unknown line, a
voided line and a closed order are all rejected with the right code;
and the +/− stepper in a real `pos` browser increases/decreases a line
and disables "−" at 1.

## Open questions

- No manager gate — see "Permissions" above; revisit if real usage shows
  quantity edits being used to under-ring what was actually delivered.
- No lower bound on how large a quantity can be set to (no upper bound
  either, same as `AddLine`) — not a problem seen in practice yet.
