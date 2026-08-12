# Floor-plan editor: table and room CRUD, seating groups, multiple floors

> **Status:** 🚧 in progress — table/room CRUD (FLR-03), seating groups (FLR-05) and multi-floor support (FLR-07) are built; the drag-and-drop canvas FLR-03's own backlog title names is not
> **Module:** Floor
> **Roadmap:** I1 (pulled forward)

## What it is

`admin` can add, edit and remove tables within a room, and add, rename
and remove rooms themselves — the data-mutation half of a floor-plan
editor. It is not yet the drag-and-drop canvas a floor-plan editor
usually means: position and shape are plain numeric/select form fields,
not something a manager drags around on screen.

Two later additions extend the same floor plan rather than standing on
their own: staff can push 2+ free tables together into one seating unit
for a large party (FLR-05), and a room can be tagged with which
physical storey it sits on so a multi-storey restaurant's floor plan
can group by floor (FLR-07).

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

**Seating groups (FLR-05) are a plain field, not a new entity.**
`Table.GroupId` is a shared `Guid?` — no separate `TableGroup` row,
because every field a group needs (which tables, combined seats) is
computable from the member tables themselves, the same reasoning
FLR-07 later applies to floor levels too. Grouping was deliberately
given real teeth rather than staying a cosmetic tag: `Table.Occupy()`
itself refuses a grouped table (`floor.table_grouped`). Without that,
a grouped table would still show as `Free` with nothing stopping a
host seating a different party there individually — actively
contradicting the reason grouping exists. Cascading `Occupy`/`Clear`/
`Release` across a group's siblings (so opening one order seats the
whole group at once) was deliberately **not** built — a materially
larger change touching every already-shipped table-state endpoint,
left as a named, deferred gap. Grouping requires every member table to
already be `Free`; a table already in another group must be
ungrouped first, never silently moved between groups.

**Floor level (FLR-07) is a display concern, not an access one.**
`Room.FloorLevel` (default `0`, ground floor; positive above it,
negative below) changes nothing about how a table behaves — only
whether a "Floor N" badge or heading renders at all. That framing
decided the UI shape directly: both `admin`'s room editor and `pos`'s
table picker stay entirely unchanged, byte-for-byte, until a tenant's
own rooms actually span more than one level, since every restaurant
seeded so far is single-storey and showing "Floor 0" on every room
when there is only ever one floor would be pure noise. Once ambiguity
does exist, *every* room gets labelled, not just the odd one out — a
bare, unbadged room would otherwise be a second, silent way to mean
"ground floor," indistinguishable from "this screen doesn't support
floors yet."

## Behaviour

1. Admin adds a table: `POST /rooms/{roomId}/tables` with
   `{ label, seats, positionX, positionY, shape }`. Starts `Free`.
2. Admin edits a table: `PUT /tables/{id}` — same shape, any `TableState`.
3. Admin removes a table: `DELETE /tables/{id}` — only while `Free`.
4. Admin adds a room: `POST /rooms` with `{ name, displayOrder, floorLevel }` (`floorLevel` defaults to `0`).
5. Admin renames/reorders/moves a room to a different floor: `PUT /rooms/{id}`.
6. Admin removes a room: `DELETE /rooms/{id}` — only while it has no tables; remove its tables first.
7. Staff push 2+ free tables together for a large party: `POST /table-groups` with `{ tableIds }`. Every member table must already be `Free`.
8. Any grouped table now refuses `POST /orders` (`floor.table_grouped`) until the group is dissolved.
9. Staff dissolve a seating group: `DELETE /table-groups/{groupId}` — every member table returns to individually-seatable `Free`.
10. `pos`'s table picker is unaffected by any of this beyond seeing the
    updated `GET /floor` result — no code there assumes the floor plan is
    static. Once `GET /floor` shows rooms on more than one `FloorLevel`,
    the picker groups them under a floor heading; otherwise it renders
    exactly as before.

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
| Table group has fewer than 2 distinct table ids | Rejected | `400 floor.table_group_too_small` |
| Table group references an unknown table | Rejected | `404 floor.table_not_found` |
| Table group targets a table that isn't `Free` | Rejected | `409 floor.table_not_free` |
| Table group targets a table already in another group | Rejected — ungroup it first, this never moves a table between groups | `409 floor.table_already_grouped` |
| `POST /orders`/`DELETE /tables/{id}` targets a grouped table | Rejected | `409 floor.table_grouped` |
| Deleting an unknown table group | Rejected | `404 floor.table_group_not_found` |

## Data

`Room` (`Name`, `DisplayOrder`, `FloorLevel` — FLR-07, default `0`) and
`Table` (`RoomId`, `Label`, `Seats`, `PositionX`/`PositionY`, `Shape`,
`State`, `GroupId` — FLR-05, a plain `Guid?` with no FK, the same
opaque-reference convention `OrderLine.MenuItemId` uses) — both owned
by `Floor`; position/shape existed before this feature, `FloorLevel`
and `GroupId` are the only new columns, both nullable/defaulted so no
existing row needed a data migration. `Table` has `xmin` optimistic
concurrency (Postgres system column, no migration); `Room` does not —
no known concurrent-edit race analogous to `Table.Occupy()` exists for
a room's own fields.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/rooms/{roomId}/tables` | Add a table to a room |
| `PUT` | `/tables/{tableId}` | Edit a table's label/seats/shape/position |
| `DELETE` | `/tables/{tableId}` | Remove a table (must be `Free`, not grouped) |
| `POST` | `/rooms` | Create a room (`floorLevel` optional, defaults to `0`) |
| `PUT` | `/rooms/{roomId}` | Rename, reorder or move a room to a different floor |
| `DELETE` | `/rooms/{roomId}` | Remove a room (must have no tables) |
| `POST` | `/table-groups` | Push 2+ free tables into one seating group (FLR-05) |
| `DELETE` | `/table-groups/{groupId}` | Dissolve a seating group, freeing every member table individually |

All take `Idempotency-Key` like every other mutation. Create/edit return
the affected `TableDto`/`RoomDto`; delete returns `204 No Content`.
`POST /table-groups` returns every affected `TableDto` (with the shared
`GroupId` now set).

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

`table-groups.spec.ts` (FLR-05) — grouping 2 free tables makes
`POST /orders` 409 on either one; deleting the group restores ordinary
seating, proven with a real order open+close afterward, not just a
`GET /floor` re-read; fewer than 2 tables, an unknown table, a
non-`Free` table, a table already in another group, and deleting an
unknown group are all rejected with their own codes.

`floor-multi-level.spec.ts` (FLR-07) — `floorLevel` round-trips through
create/`GET /floor`/update; omitting it on create defaults to `0`;
creating a second-floor room makes `admin`'s "Floor N" badge appear on
every room, including the seeded ground-floor ones; editing a room's
floor level through the real UI round-trips to the API; `pos`'s table
picker renders both floor headings with rooms grouped correctly under
each.

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
- **FLR-05:** opening one order that seats an entire group at once is
  not built — a party at a pushed-together table is still seated by
  opening an order against one specific table id, same as always.
  Cascading `Occupy`/`Clear`/`Release` across a group's siblings would
  touch several already-shipped, already-tested endpoints and deserves
  its own careful pass.
- **FLR-05:** no admin UI exists to create or dissolve a seating group
  yet — no floor-plan multi-select exists in either web client today,
  so this ships API-only, verified at that level.
- **FLR-07:** floor groups are sorted ascending by `FloorLevel` in
  `pos`'s picker (so a basement at `-1` would render before ground
  floor `0`) — untested against any real multi-storey restaurant's own
  preferred ordering, since none exists yet.
