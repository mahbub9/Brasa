# Combos (menu do dia)

> **Status:** ✅ built, API-level only — no ring-up UI in `pos`/`admin` yet
> **Module:** Catalog (composes Ordering to ring one up)
> **Roadmap:** I1 (pulled forward)

## What it is

A named, fixed-price bundle of specific menu items — soup + main + drink
for one price lower than the sum of their standalone prices, the everyday
*menu do dia* a Portuguese restaurant runs. `admin`/`pos` have no UI for
this yet; it's built and verified at the API level, ready for whichever
screen picks it up.

## Why it works this way

**A combo is never a new fiscal concept.** Portuguese VAT is charged on
the underlying goods, not on a bundle wrapper — a combo mixing a 13% main
and a 23% drink cannot legally collapse into one line at one rate. Ringing
one up (`POST /orders/{id}/combo-lines`) allocates the combo's fixed
`Price` across its components via `Money.Allocate`, weighted by each
component's own current standalone price, then adds each component as an
**ordinary `OrderLine`** at its own real VAT rate. A combo is therefore a
priced-lower bundle of ordinary lines, never a special case the fiscal
document builder has to know about.

**The same proration `ORD-11` already uses for an order-level discount.**
`Money.Allocate` weighted-by-price is exactly the tool an order-level
discount is prorated across lines with — reused here rather than inventing
a second allocation algorithm for what is structurally the same problem:
divide a fixed amount across several items proportionally, with the
remainder cent landing deterministically (see `Money.Allocate`'s own doc
comment) rather than being dropped or duplicated.

**Weighted by `EffectivePrice`, not the raw `Price`.** A component with a
scheduled price change due (CAT-16) pulls its fair, smaller share of the
combo's fixed price — the same figure `GET /menu` would show for it
standalone at that instant, not a stale list price.

**A narrow first slice, the same shape CAT-05/IDN-01 shipped as.** Each
component is exactly one unit of one specific item — not a guest's choice
among several ("any starter"), not a quantity greater than one. Both are
real product features, deliberately deferred rather than guessed at. No
rename, delete, or remove-component either.

## Behaviour

1. Admin creates an empty combo: `POST /combos` with `{ name, price }`.
2. Admin adds components, one item at a time: `POST /combos/{id}/components`
   with `{ menuItemId }`. Rejects a second entry for the same item.
3. Ringing it up on an open order: `POST /orders/{id}/combo-lines` with
   `{ comboId }`. Resolves every component's current `EffectivePrice`,
   allocates the combo's fixed price across them by that weight via
   `Money.Allocate`, and adds one `OrderLine` per component at its own
   `VatRate` — no single "combo line" ever appears on the order.
4. Example: a €6.00 combo with a €4.00 (13%) soup and a €3.00 (23%) wine —
   weights `[400, 300]` minor units — allocates to €3.43/€2.57 exactly
   (`Money.Allocate`'s deterministic remainder rule puts the leftover cent
   on the first weight). The pre-bill's VAT breakdown shows both bands
   separately, reconciling to the same €6.00.
5. Every component must currently be `IsAvailable`; if the combo's price
   can't be allocated at all (every component's `EffectivePrice` is zero),
   ringing it up is refused rather than dividing by zero.

## Offline behaviour

Not applicable — cloud API endpoints, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Combo `name` missing/empty on create | Rejected | `400 catalog.invalid_combo_name` |
| Combo `price` negative on create | Rejected | `400 catalog.invalid_price` |
| Adding a component that's already on the combo | Rejected | `409 catalog.combo_component_exists` |
| Adding a component naming an unknown menu item | Rejected | `404 catalog.item_not_found` |
| Adding a component to an unknown combo | Rejected | `404 catalog.combo_not_found` |
| Ringing up a combo with zero components | Rejected | `400 catalog.combo_has_no_components` |
| Ringing up a combo with an unavailable component | Rejected | `409 catalog.item_unavailable` |
| Ringing up an unknown combo | Rejected | `404 catalog.combo_not_found` |
| Every component's effective price is zero, nothing to allocate against | Rejected | `409 catalog.combo_price_not_allocable` |
| `GET /combos/{id}` for an unknown combo | Rejected | `404 catalog.combo_not_found` |

## Data

`Combo` (`Name`, `Price`) and `ComboComponent` (`ComboId`, `MenuItemId`,
one row per component) — both owned by Catalog, mirroring
`PriceList`/`PriceListEntry`'s own ownership shape. Nothing is persisted
about a rung-up combo beyond the ordinary `OrderLine` rows it produced —
there is no `ComboLine` entity or any record linking those lines back to
the combo that created them, the same "order lines copy their own values,
never a live join" convention every other Ordering line already follows.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/combos` | Create an empty combo |
| `GET` | `/combos` | List every combo for the tenant |
| `GET` | `/combos/{comboId}` | Get one combo and its components |
| `POST` | `/combos/{comboId}/components` | Add one component item |
| `POST` | `/orders/{orderId}/combo-lines` | Ring the combo up as ordinary lines on an open order |

All mutations take `Idempotency-Key` like every other endpoint.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None directly — a combo holds no fiscal logic of its own. The lines it
produces are ordinary `OrderLine`s, each carrying its own real VAT rate,
so the existing fiscal document builder (`BuildFiscalLines`) itemizes them
exactly as it would if a guest had ordered the same items individually.

## Permissions

None enforced — same "ships ahead of manager authorisation" shape every
other Catalog/Ordering mutation in this codebase has today.

## Testing

`combos.spec.ts` — two components at different VAT bands (13%/23%) and
different standalone prices (4.00/3.00, a 4:3 weight ratio, deliberately
not an even split) allocate to €3.43/€2.57 exactly and reconcile to the
combo's own €6.00; the pre-bill's VAT breakdown shows both bands
separately, still reconciling to the cent. An empty name and a negative
price are rejected on create; a duplicate component, an unknown item, and
an unknown combo are all rejected on add-component with their own codes;
ringing up an empty combo, an unavailable component, and an unknown combo
are all rejected with their own codes; a fresh `GET` 404s for an unknown
combo; components persist across a refetch. Uses freshly-imported test
items via the CSV pipeline, isolated from the shared seeded menu other
specs read concurrently.

## Open questions

- No "ring up this combo" UI in `pos` or `admin` yet — verified at the API
  level only, the same "mechanism before the trigger" shape CAT-14/15
  already established.
- No guest choice among alternatives ("any starter") and no
  quantity-greater-than-one component — both real product features,
  deliberately deferred rather than guessed at.
- No rename, delete, or remove-component endpoint yet — the same narrow
  "create + add only" first slice several other Catalog/Identity features
  shipped as.
