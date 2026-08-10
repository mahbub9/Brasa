# Floor-plan editor: table and room CRUD

> **Status:** 🚧 in progress — table and room CRUD are built; the drag-and-drop canvas this feature's own backlog title (FLR-03) names is not
> **Module:** Floor
> **Roadmap:** I1 (pulled forward)

## What it is

`admin` can add, edit and remove tables within a room, and add, rename
and remove rooms themselves — the data-mutation half of a floor-plan
editor. It is not yet the drag-and-drop canvas a floor-plan editor
usually means: position and shape are plain numeric/select form fields,
not something a manager drags around on screen.

## Why it works this way

**Position and shape existed since I1 with nothing to set them.**
`Table.PositionX`/`PositionY`/`Shape` were seeded onto the entity from
the start, with `Table`'s own remarks saying explicitly that they exist
"so a future drag-and-drop editor (FLR-03) has somewhere to persist to
without a schema change." This feature is that persistence layer,
built the moment there was a real reason to (an admin screen that
needs *some* way to set them) — the canvas itself is a separate,
larger, still-open piece of work.

**The same "mechanism before the visual affordance" call as the menu
editor.** WEB-10 shipped category/item toggles and an inline price
editor with no drag-and-drop menu layout; this ships table/room
add/edit/delete with no drag-and-drop floor layout. Same reasoning both
times: the mutation is the part that unblocks a real restaurant from
using the system, the visual polish is a separable, later improvement.

**Table deletion is hard, not soft — the opposite of `MenuItem` (CAT-18).**
A closed order's `TableLabel` is already snapshotted at the moment the
order was opened (`Order.TableLabel`), so nothing ever needs to
re-resolve a deleted table's row the way a receipt re-derives a menu
item's name from `OrderLine.MenuItemId`. `Order.TableId` stays a plain,
possibly-dangling reference on old closed orders, which is fine — it is
never live-joined, only resolved fresh for *current* state (is this
table still occupied?), and a closed order never asks that question
again.

**Deleting a table requires `Free`; deleting a room requires zero
tables.** A table mid-service, or dirty and awaiting clearing, must not
simply vanish from under a live order — so delete reuses the exact same
guard shape `Occupy()`/`Clear()` already established
(`floor.table_not_free`). A room's guard is checked at the API layer,
not the domain: `Room` has no navigation collection to `Table` (the
same sibling-entity shape every other Floor pair uses — see
`docs/architecture/module-boundaries.md` on why Ordering references
Floor the same opaque way), so there is nothing for `Room` itself to
check. `DeleteRoomAsync` queries `Tables` directly instead.

## Behaviour

1. Admin adds a table: `POST /rooms/{roomId}/tables` with
   `{ label, seats, positionX, positionY, shape }`. Starts `Free`.
2. Admin edits a table: `PUT /tables/{id}` — same shape, any `TableState`.
3. Admin removes a table: `DELETE /tables/{id}` — only while `Free`.
4. Admin adds a room: `POST /rooms` with `{ name, displayOrder }`.
5. Admin renames/reorders a room: `PUT /rooms/{id}`.
6. Admin removes a room: `DELETE /rooms/{id}` — only while it has no tables; remove its tables first.
7. `pos`'s table picker is unaffected by any of this beyond seeing the
   updated `GET /floor` result — no code there assumes the floor plan is
   static.

## Offline behaviour

Not applicable — cloud API endpoints, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Table `label` missing/empty | Rejected, nothing changed | `400 floor.invalid_label` |
| Table `seats` below 1 | Rejected | `400 floor.invalid_seats` |
| Table `shape` not a recognised name | Rejected | `400 floor.invalid_shape` |
| Table create targets an unknown room | Rejected | `404 floor.room_not_found` |
| Table edit/delete targets an unknown table | Rejected | `404 floor.table_not_found` |
| Table edit races another writer (`xmin`) | Rejected — genuine lost-update, no state to re-check | `409 floor.table_concurrently_modified` |
| Table delete targets a non-`Free` table | Rejected, initially or via the same `xmin` race | `409 floor.table_not_free` |
| Room `name` missing/empty, on create or rename | Rejected | `400 floor.invalid_room_name` |
| Room edit/delete targets an unknown room | Rejected | `404 floor.room_not_found` |
| Room delete targets a room that still has tables | Rejected | `409 floor.room_not_empty` |

## Data

`Room` (`Name`, `DisplayOrder`) and `Table` (`RoomId`, `Label`, `Seats`,
`PositionX`/`PositionY`, `Shape`, `State`) — both owned by `Floor`, no
new columns for this feature (position/shape already existed). `Table`
has `xmin` optimistic concurrency (Postgres system column, no
migration); `Room` does not — no known concurrent-edit race analogous
to `Table.Occupy()` exists for a room's own fields.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/rooms/{roomId}/tables` | Add a table to a room |
| `PUT` | `/tables/{tableId}` | Edit a table's label/seats/shape/position |
| `DELETE` | `/tables/{tableId}` | Remove a table (must be `Free`) |
| `POST` | `/rooms` | Create a room |
| `PUT` | `/rooms/{roomId}` | Rename or reorder a room |
| `DELETE` | `/rooms/{roomId}` | Remove a room (must have no tables) |

All take `Idempotency-Key` like every other mutation. Create/edit return
the affected `TableDto`/`RoomDto`; delete returns `204 No Content`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. Purely a floor-plan/operations concern.

## Permissions

None enforced yet — same "ships ahead of manager authorisation" shape
as the menu editor's mutations (CAT-01/13/19). IDN-11 is the eventual
real gate for admin-editing actions in general, once staff accounts and
roles exist.

## Testing

`floor-table-management.spec.ts` — table CRUD: create/edit/delete
round-trip through `GET /floor`; an empty label, zero seats, an
unrecognised shape and an unknown room are all rejected on create; an
unknown table 404s on edit/delete; deleting a non-`Free` table 409s; the
`admin` UI round trip (add → edit → delete) works in a real browser.
Room CRUD: the same shape for rooms — create/rename/delete via the API
and the `admin` UI, an empty name rejected on create/rename, an unknown
room 404s, deleting a non-empty room 409s.

## Open questions

- The drag-and-drop canvas itself — this feature's own backlog title
  (FLR-03) names it directly; position/shape are editable today only as
  plain form fields, not by dragging a table on screen.
- No bulk operations (e.g. duplicate a table, reorder several rooms at
  once) — not needed yet at the scale a single restaurant's floor plan
  operates at.
- `DisplayOrder` collisions between rooms (two rooms both claiming
  position 0) aren't validated or auto-resolved — last write wins on
  sort order, same as it silently would have before this feature existed.
