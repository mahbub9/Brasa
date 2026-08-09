# Void a line

> **Status:** 🚧 in progress — the void mechanism is built, manager authorisation (named in this feature's own backlog title, ORD-10) is not
> **Module:** Ordering
> **Roadmap:** I2 (pulled forward)

## What it is

Staff can cancel a single line after it's already been rung up — a dish
that came out wrong, or was never actually served — with a required
reason. The line isn't deleted: what was ordered stays visible, only its
contribution to the bill drops to zero.

## Why it works this way

**No manager-authorisation gate yet, despite the backlog row's own title
naming one.** Real POS voiding is normally gated behind a manager PIN,
precisely because an unrestricted void is a shrinkage vector. That gate is
IDN-11, and IDN doesn't exist yet. This ships the mechanism ahead of the
trigger — the same shape as [Discounts](discounts.md) (ORD-11) and CAT-13's
86-ing — so the gate can be wired on top later without touching this
code's shape.

**A reason is required, not optional.** A void with no reason is exactly
the kind of gap future shrinkage-detection (DIF-13) would need to close —
rejecting it outright at the point of voiding is cheaper than trying to
reconstruct intent later from an empty field.

**The line is never deleted.** `ItemName`, `UnitPrice`, `Quantity` and
`Modifiers` all stay exactly as they were when the line was rung up — an
audit trail of what was ordered and then cancelled, and why. Only
`LineTotal` changes, dropping to zero regardless of any discount that was
also on the line.

**A voided line is omitted from the fiscal document entirely, not
rendered as a 100%-discount line.** [Discounts](discounts.md) render as a
negative `FiscalDocumentLine` because the item *was* delivered, just at a
reduced price. A voided line was never delivered — from the invoice's
point of view it never happened, so it simply doesn't appear. It still
appears on the pre-bill (built from the order's own lines directly, not
from the fiscal-line builder) so staff retain visibility into what was
cancelled before the guest pays.

**Voiding every line on an order fails the close, but at the fiscal
layer, not the order layer.** `Order.Close()`'s own guard only checks
`_lines.Count`, which doesn't distinguish voided from live lines — a
fully-voided order still passes it. But the fiscal-line builder
(`BuildFiscalLines`, shared with discounts) omits every voided line, so
`IFiscalProvider.IssueSimplifiedInvoiceAsync` receives an empty list and
its own pre-existing `fiscal.no_lines` guard rejects the whole close.
Because the API layer doesn't persist `Close()`'s in-memory transition
until the fiscal document is actually issued, the order is left genuinely
`Open` in the database — not silently closed with nothing to show for it.
This wasn't designed in up front; it fell out of composing two guards
that already existed for unrelated reasons, and it's correct.

## Behaviour

1. Staff voids a line: `POST /orders/{id}/lines/{lineId}/void` with
   `{ "reason": "Guest changed their mind" }`.
2. The response shows the line with `isVoided: true`, the reason, and
   `lineTotal: 0` — `itemName`/`unitPrice`/`quantity` are unchanged.
3. The order's `total` drops by exactly what that line was contributing
   (including any discount it already had — voiding a discounted line
   still zeroes it, doesn't first "undo" the discount).
4. A pre-bill generated afterward still lists the voided line (so staff
   can see it was cancelled) but its zero contributes nothing to the
   total or the VAT breakdown.
5. Closing the order issues a fiscal document that never mentions the
   voided line at all.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Reason missing or blank | Rejected, nothing changed | `400 order.void_reason_required` |
| Unknown line id | Rejected | `404 order.line_not_found` |
| Line already voided | Rejected | `409 order.line_already_voided` |
| Void attempted on a closed/merged order | Rejected | `409 order.not_open` |
| Every line on the order gets voided, then close is attempted | `Order.Close()` succeeds in memory, but the fiscal issuance step rejects it and the transition is never persisted | `400 fiscal.no_lines`; the order is still `Open` and can take more lines |

## Data

`OrderLine.IsVoided` (bool), `VoidReason` (string?, ≤300 chars), `VoidedAtUtc`
(nullable UTC timestamp) — all owned by Ordering, all on the existing
`order_lines` table (`AddOrderLineVoid` migration). `LineTotal` becomes a
three-way derived value: zero when voided, otherwise gross-less-discount,
same as before ORD-10 existed.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/orders/{orderId}/lines/{lineId}/void` | Void a line, with a required reason |

Takes `Idempotency-Key` like every other mutation. Returns the full
`OrderDto`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

Yes, directly — a voided line is excluded from the issued document's
totals. Shares `BuildFiscalLines` with [Discounts](discounts.md), so the
same `order.Total == document.GrossTotal` reconciliation invariant that
feature depends on holds here too, verified live with a voided line in
the mix. Not certification-relevant work itself (no new document type),
but exactly the kind of behaviour an accountant reviewing sample
documents would want to see demonstrated.

## Permissions

None enforced yet — see "Why it works this way" above. This is the
single biggest open gap on this feature specifically, since its own
backlog title names manager authorisation as in scope.

## Testing

`void-line.spec.ts` — API-level only, no `pos`/`admin` UI exists yet.
Covers: voiding zeroes a line while preserving its snapshot data; a
voided line vanishes from the issued fiscal document but stays on the
pre-bill, with the VAT-breakdown/total reconciliation invariant intact; a
missing/blank reason, an unknown line, a double-void and a void on a
closed order are all rejected with the right code; and — the edge case
that surfaced during development, not one written in for coverage's sake
— voiding every line on an order fails the close with `fiscal.no_lines`
while leaving the order genuinely `Open`, not corrupted into a
half-closed state.

## Open questions

- Manager authorisation (IDN-11) — the real gate for this feature,
  tracked separately since it needs staff accounts and roles that don't
  exist yet.
- `SplitByItem`'s by-item preview doesn't account for a voided line (same
  documented gap as line/order discounts) — a voided line can still be
  allocated a share at its full original price by that one method.
- No reason taxonomy (free text only) — a fixed list of common reasons
  might matter for later shrinkage reporting (DIF-13), not decided here.
