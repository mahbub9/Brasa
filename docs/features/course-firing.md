# Course firing

> **Status:** ✅ built
> **Module:** Ordering
> **Roadmap:** I2 (pulled forward)

## What it is

Staff can send an order's lines to the kitchen — a whole course at once
("fire the starters"), or everything still pending regardless of course
in one call ("fire all"). No real kitchen exists yet (ESC/POS printing
and the KDS are I4), so firing today only records that it happened; it
is the seam a future ticket-printing consumer will read from.

## Why it works this way

**`Course.cs` named this task before it existed.** CAT-14 added
`MenuItem.Course` (`Starter`/`Main`/`Dessert`/`Drink`) with a doc comment
reading "Course firing (ORD-07) isn't built yet; this is the tag it will
read from once it is." This feature is that promise kept, not a
coincidence of naming.

**`OrderLine.Course` is a snapshot, not a live join — same convention as
`ItemName`/`UnitPrice`/`VatRateFraction`.** A line copies the item's
course at the moment it's rung up. If a manager reassigns the item to a
different course on the menu afterward, an already-open line keeps
firing under the course it was actually ordered with — the exact
reasoning `docs/architecture/module-boundaries.md` gives for every other
snapshotted field. Ordering still never references Catalog's `Course`
enum directly (module boundaries hold): the API layer resolves and
validates the course name before it ever reaches `Order.AddLine`.

**One endpoint does both "partial" and "full."** ORD-08's title names
two sends; rather than two routes, `POST /orders/{id}/fire` takes an
optional `course` — a name fires only that course's own unfired lines,
`null` fires everything still pending. The two cases share one domain
method (`Order.FireLines`) because they're the same operation with a
different filter, not two different operations.

**Firing is idempotent.** An already-fired line is silently skipped,
never re-fired and never an error. Firing the same course twice, or
calling "fire all" after a specific course was already sent, both just
do less work the second time — a UI can retry a fire action after a
flaky connection without worrying about double-counting anything (there
is nothing financial to double-count, but a kitchen ticket printed twice
would be a real annoyance once printing exists).

**`OrderLine.IsFired`/`FiredAtUtc` is deliberately the only status
tracked.** ORD-09's title says "order line status tracking," which could
mean a richer state machine (`Preparing`/`Ready`/`Served`). Those
transitions need something on the other end to drive them — a KDS bump
button, a timer — and none of that exists yet (KIT-10…13 are I4). Adding
states nothing can ever set would be exactly the kind of guessed-at
scope this codebase avoids elsewhere (see channel pricing's "delivery
out of scope" note). `IsFired` is the one state meaningful today.

**No manager authorisation, unlike void (ORD-10) and discounts
(ORD-11).** Both of those gate something that changes what a guest
is charged or removes billable history. Firing changes neither — it is
purely a kitchen-workflow signal. Gating it behind a PIN would be
friction with no corresponding risk to justify it.

## Behaviour

1. Staff rings up lines across two courses — a starter, then a main.
   Each line's `course` is set from the menu item automatically; nothing
   extra is sent when adding the line.
2. Staff fires the starters: `POST /orders/{id}/fire` with
   `{ "course": "Starter" }`. Only the starter line(s) get
   `isFired: true`/`firedAtUtc` set; the main stays pending.
3. Later, staff fires everything still pending: `POST /orders/{id}/fire`
   with `{ "course": null }`. The main line (and anything with no course
   assigned at all) is now fired too.
4. `pos`'s order summary shows a button per course that still has an
   unfired line, plus a "Fire all" button — both disappear together once
   nothing is left pending. A fired line carries a small "Sent" badge.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| `course` isn't a recognised name | Rejected, nothing changed | `400 order.invalid_course` |
| Order id doesn't exist | Rejected | `404 order.not_found` |
| Order isn't `Open` (closed or merged) | Rejected | `409 order.not_open` |
| A line is voided | Silently excluded from firing, not an error | Nothing — the voided line simply never gets `isFired: true` |
| A line has no course and `course` names one | Silently excluded — no course value could ever match | Nothing — only `course: null` ("fire all") ever reaches it |

## Data

`OrderLine.Course` (nullable string, max 20 chars — long enough for any
of Catalog's four course names with room to spare), `IsFired` (bool,
required, default `false`), `FiredAtUtc` (nullable UTC timestamp) — all
on the existing `order_lines` table (`AddOrderLineCourseAndFiring`
migration, purely additive).

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/orders/{orderId}/fire` | Fires a named course, or (`course: null`) every unfired line |

Takes `Idempotency-Key` like every other mutation. Returns the full
`OrderDto`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. `Course`/`IsFired`/`FiredAtUtc` never reach `BuildFiscalLines` or
`IFiscalProvider` — purely a kitchen-workflow concern.

## Permissions

None — see "Why it works this way" above. Unlike [void](void-a-line.md)
and [discounts](discounts.md), this isn't a gap waiting to be closed;
firing has no financial consequence to gate.

## Testing

`order-course-firing.spec.ts` — firing a named course leaves a different
course's own line untouched; firing all afterward sends the rest; firing
an already-fired course again is a no-op; a voided line is never fired,
even by "fire all"; a line with no course assigned only ever fires with
"fire all," never a named course; an unrecognised course name, a closed
order, and an unknown order id are each rejected with their own code;
and the `pos` UI fires a course through a real browser, the badge
appearing and the fire-controls bar disappearing once nothing is left
pending.

## Open questions

- Real kitchen routing (KIT-01…09) and a KDS (KIT-10…13) — both I4. This
  feature is the seam they'll read `IsFired`/`FiredAtUtc` from, not a
  replacement for either.
- Richer per-line status (`Preparing`/`Ready`/`Served`) — deliberately
  not built until something exists to drive those transitions.
- No UI signal yet for *when* a line was fired (`firedAtUtc` is returned
  but not rendered) — useful once ageing/prep-timer display matters,
  which needs the KDS this feature is waiting on.
