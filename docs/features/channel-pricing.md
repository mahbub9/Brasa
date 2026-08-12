# Channel pricing — dine-in vs takeaway

> **Status:** 🚧 in progress — dine-in/takeaway built, delivery not (this feature's own backlog title names all three)
> **Module:** Catalog
> **Roadmap:** I1 (pulled forward)

## What it is

A menu item can be priced differently for a takeaway/counter-sale order
than for a dine-in one — a common practice for restaurants with no table
service to cover on a takeaway order. Setting a takeaway price is
optional; leaving it unset means "charge the same as dine-in."

## Why it works this way

**Null means "same as dine-in," not "free."** Same convention as
`MenuItem.Course`/`Station` (CAT-14/15): an unset `TakeawayPrice` is a
data-entry gap, never a claim that the item costs nothing for takeaway.

**VAT rate is deliberately untouched.** This feature's own title says
"channel *pricing*," not "channel tax." Per-channel VAT resolution is a
separate concern — [tax rules](tax-rules.md) (CAT-07/08) — built since,
but deliberately not yet wired into `AddLine`: rewiring the live VAT
computation path is its own fiscally-sensitive task, not a side effect
of either feature. `VatRate` stays the flat rate `MenuItem` carries
live; conflating the two here would mean guessing at a VAT rule this
codebase's own hard rules say must wait for accountant confirmation
(`docs/fiscal/README.md`). `AddLineAsync` copies `VatRateFraction` onto
the line unchanged regardless of which price won.

**Delivery is explicitly out of scope, not an oversight.** This
feature's backlog row names three channels; only two are built. There is
no delivery order path anywhere in this codebase yet — no channel flag
on `Order` for it, no endpoint, nothing — so there is nothing for a
delivery price to attach to. Same shape as CAT-17's "CSV only, Excel not
supported" caveat: an honest, named gap rather than a half-built guess
at what delivery ordering might eventually look like.

**A snapshot, not a live join, same as `Price`.** `OrderLine.UnitPrice`
already snapshots whichever price won at the moment a line is rung up
(`AddLineAsync` picks `TakeawayPrice ?? Price` when `Order.IsTakeaway`).
Changing either price afterward never rewrites an already-open line —
the exact guarantee `Reprice` already gives the dine-in price, extended
to the new field for free because both go through the same snapshot
point.

## Behaviour

1. Staff sets a takeaway price: `PUT /menu/items/{itemId}/takeaway-price`
   with `{ "price": 4.00 }` (or `{ "price": null }` to clear it back to
   "same as dine-in").
2. `GET /menu` and `GET /menu/all` both return `takeawayPrice` alongside
   `price` for every item.
3. Ringing up an item onto a **takeaway** order (`Order.IsTakeaway`)
   charges `takeawayPrice` when one is set, `price` otherwise.
4. Ringing up the same item onto a **dine-in** order always charges
   `price`, regardless of whether a takeaway price exists.
5. `pos`'s menu grid shows whichever price the order actually in
   progress would charge — the button a waiter taps must never show a
   number different from what gets rung up.
6. `admin`'s menu editor shows an inline add/edit/clear control next to
   the existing dine-in price field.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Negative price | Rejected, nothing changed | `400 catalog.invalid_price` |
| Unknown item id | Rejected | `404 catalog.item_not_found` |

## Data

`MenuItem.TakeawayPrice` — nullable `Money`, mapped via a new
`MapOptionalMoney` (`ModelBuilderExtensions.cs`), the same two-column
(minor units + currency) shape as `MapMoney` but with the whole complex
property marked `IsRequired(false)` so both columns are nullable
together. `AddMenuItemTakeawayPrice` migration.

## API

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/menu/items/{itemId}/takeaway-price` | Set or clear a menu item's separate takeaway price |

Takes `Idempotency-Key` like every other mutation. Returns the full
`MenuItemDto`. Decimal major-units convention (`4.00`), matching
`UpdateMenuItemPriceRequest` and the CAT-17 CSV import convention — not
a full `MoneyDto`, since EUR is the only currency this system represents
today.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

Indirect only. The price snapshotted onto a line (whichever one won)
flows into `BuildFiscalLines`/`IFiscalProvider` exactly like the dine-in
price always has — no new document type, no change to VAT derivation.
Worth an accountant's attention eventually only insofar as differential
channel pricing itself is: this codebase makes no claim about whether
Portuguese consumer-pricing law treats a takeaway price differently from
a dine-in one beyond VAT (which this feature doesn't touch).

## Permissions

None enforced yet — same "ships ahead of manager authorisation" shape
as repricing (CAT-19), 86-ing (CAT-13) and category visibility (CAT-01).
IDN-11 is the eventual real gate for menu-editing actions in general,
once staff accounts and roles exist.

## Testing

`menu-item-takeaway-price.spec.ts` — sets/persists/clears a takeaway
price via the API; rejects a negative price and an unknown item; a
takeaway order rings up the takeaway price while a dine-in order for
the exact same item still charges the dine-in one; the `pos` menu
button shows the correct price for whichever kind of order is actually
in progress; and the `admin` inline editor's add/edit/clear round-trips
to the real API, not just local component state.

## Open questions

- Delivery pricing (the third channel this row's own title names) —
  blocked on a delivery order path existing at all, not just a price
  field.
- Per-channel VAT ([tax rules](tax-rules.md), CAT-07/08) — the model and
  resolution service exist now, but `AddLine` still resolves VAT from
  `MenuItem.VatRate` directly; wiring the two together is its own
  fiscally-sensitive task, deliberately not done here.
- No UI signal yet for *why* a takeaway price might legitimately be
  higher than dine-in (packaging cost) rather than lower (no service) —
  left as free-form staff judgement, same as any other price.
