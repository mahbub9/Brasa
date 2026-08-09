# Menu item classification: course and station

> **Status:** ✅ built
> **Module:** Catalog
> **Roadmap:** I1 (pulled forward)

## What it is

A menu item can declare which **course** it's normally served at
(`Starter`/`Main`/`Dessert`/`Drink`) and which **kitchen station**
prepares it (`Grill`/`Bar`/`ColdKitchen`/`Fryer`/`Pastry`). Both are
independent of `MenuCategory`, and independent of each other.

## Why it works this way

**Course and category are not the same axis, even though this project's
own seed data makes them look identical.** `MenuCategory` is how the menu
is organised for *browsing* — a restaurant might just as easily group by
ingredient ("Peixe" / "Carne") as by course. `Course` is *when* a dish is
fired to the kitchen. A fish main and a fish starter can share a category
and still need different courses. The two are tracked separately so a
future course-firing feature (ORD-07) doesn't have to assume the seed
data's categories happen to line up with meal structure.

**Station is independent of both.** A starter and a main can both come
off the grill; a dessert and a drink can both come from the bar. Modelling
it as a third, orthogonal tag (rather than deriving it from course or
category) is what makes it actually useful once station routing (KIT-06)
exists — a Grill ticket needs every grilled item regardless of which
course or category it's filed under.

**Both ship with no consumer yet.** Nothing reads `Course` for firing
(ORD-07 isn't built) and nothing reads `Station` for routing (KIT-06
isn't built) — `admin`/`pos` don't even have UI for either. This is a
genuinely greenfield instance of the "ship the seam ahead of the trigger"
pattern already used for CAT-13 (86-ing) and CAT-19 (repricing), except
those two had a *dormant domain method* waiting for a caller; these two
didn't exist in any form until now.

**Both are nullable, not defaulted.** An unset `Course`/`Station` is a
data-entry gap, not a claim that the item has no course or is prepared
nowhere — the same convention `MenuItem.Allergens`' empty list already
established.

## Behaviour

1. `PUT /menu/items/{id}/course` with `{ "course": "Starter" }` sets it;
   `{ "course": null }` clears it back to unassigned.
2. `PUT /menu/items/{id}/station` works the same way, independently.
3. Both show up on every `MenuItemDto` — `GET /menu` (guest-facing) and
   `GET /menu/all` (`admin`'s unfiltered management view) alike.

## Offline behaviour

Not applicable — cloud API endpoints, `admin`-only in intent (no
offline-capable client touches menu editing).

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Unrecognised course name | Rejected, nothing changed | `400 catalog.invalid_course` |
| Unrecognised station name | Rejected, nothing changed | `400 catalog.invalid_station` |
| Unknown item id (either endpoint) | Rejected | `404 catalog.item_not_found` |

## Data

`MenuItem.Course` (`Domain.Course?`) and `MenuItem.Station`
(`Domain.KitchenStation?`) — both nullable, both stored as
string-converted enums (`character varying(20)`), the same pattern as
every other enum in this codebase (`VatRate`, `OrderStatus`, `TableState`,
ORD-11's `DiscountKind`). Two independent migrations
(`AddMenuItemCourse`, `AddMenuItemStation`).

## API

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/menu/items/{itemId}/course` | Set or clear which course an item is served at |
| `PUT` | `/menu/items/{itemId}/station` | Set or clear which kitchen station prepares an item |

Both take `Idempotency-Key` like every other mutation and return the full
`MenuItemDto`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. Course and station are staff/kitchen-workflow metadata; neither
appears on a fiscal document or affects a total.

## Permissions

None enforced yet — same gap as every other Catalog mutation until IDN
exists. Menu editing is conceptually `admin`-only, but nothing currently
stops any caller.

## Testing

`menu-item-course.spec.ts` and `menu-item-station.spec.ts` — API-level
only, no UI exists for either yet. Each covers: set, persists across a
fresh `GET`, clears; an unrecognised name and an unknown item both
rejected with the right code.

## Open questions

- No admin UI. `admin`'s menu editor (WEB-10) doesn't have a control for
  either field yet — both are only reachable via the API today.
- Whether `Course`/`Station` should become required fields (rather than
  an indefinitely-nullable data-entry gap) once ORD-07/KIT-06 actually
  need them is an open product question, not decided here.
- The exact `Course`/`KitchenStation` enum members are a reasonable
  first cut, not confirmed against a real restaurant's actual station
  layout — a kitchen with, say, a dedicated pizza station would need a
  new member added (schema-compatible, since both are stored as strings).
