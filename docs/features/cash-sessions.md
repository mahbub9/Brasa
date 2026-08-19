# Cash sessions — *abertura de caixa* with a starting float

> **Status:** ✅ built
> **Module:** Payments, composed with Identity at the API layer
> **Roadmap:** I6 (`PAY-08`, `PAY-09`, `PAY-10`, `PAY-11`)

## What it is

A staff member declares a starting cash float against a specific terminal
at the start of a shift — *abertura de caixa*, "opening the till." A new
`CashSession` record (Payments module) captures who opened it, which
terminal, how much cash was in the drawer to start, and when. Only one
session can be open per terminal at a time. `POST /cash-sessions` opens
one; `POST /cash-sessions/{id}/close` closes it; `GET
/terminals/{id}/cash-sessions/current` answers "is this terminal's till
currently open, and by whom."

While a session is open, staff can also record `CashMovement`s (PAY-09) —
pay-ins and pay-outs, cash added to or removed from the drawer mid-shift
for a reason other than an order payment (a change float top-up, petty
cash for a delivery, a bank drop). Every movement requires a reason.
`POST /cash-sessions/{id}/movements` records one; `GET
/cash-sessions/{id}/movements` lists them.

Before closing out, staff record a blind cash count (PAY-10) — what's
physically in the drawer, counted without being shown any expected total
first. `POST /cash-sessions/{id}/count` records it, at most once per
session, directly on the `CashSession` row itself.

*Fecho de caixa* variance reporting (PAY-11) closes the loop: once a cash
payment can optionally be tagged with the session it was taken under
(`Payment.CashSessionId`, threaded through `POST /orders/{id}/payments` and
`.../payments/split`), `GET /cash-sessions/{id}/variance` compares what
*should* be in the drawer (opening float + pay-ins − pay-outs + cash
payments taken under that session) against the blind count, if one exists,
and reports the difference. Nothing is stored — every field is recomputed
from `CashMovement` and `Payment` rows on each request.

## Why it works this way

**Purely a record — does not gate anything else yet.** No payment endpoint
requires an open session to exist. This is the same "mechanism before the
trigger" shape this codebase already uses everywhere — `TaxRule` shipped
before `AddLine` read it, price lists shipped before anything resolved an
effective price through them, DAT-07's `brasa_system` role shipped with no
real consumer yet. Requiring a session before a terminal can record cash
payments — or reconciling one against a blind count at close — is real
future work (PAY-10/11), deliberately not this task's scope.

**`TerminalId`/`OpenedByStaffId` are plain opaque references, not foreign
keys.** The same convention `Payment.OrderId` already uses for a Floor
`Table`: Payments never queries Identity directly (see
[module-boundaries.md](../architecture/module-boundaries.md)). The API
layer (`CashSessionEndpoints`) composes `PaymentsDbContext` and
`IdentityDbContext` to confirm both are real and to resolve display names,
the same shape `PaymentEndpoints` already uses for tip attribution (PAY-06).

**Only one open session per terminal at a time, enforced at the API
layer.** Two overlapping sessions on the same till would make "who's
responsible for this cash drawer right now" ambiguous — the entire point
of the record. `CashSession` itself has no way to see its siblings (it's
one row), so the check is a query in `OpenCashSessionAsync`
(`409 cash_session.already_open`) before a second row is ever constructed,
not a domain invariant on the type itself.

**Any staff role can open or close a session — no manager gate.** The same
"confirm the id is real, nothing more" shape `FloorEndpoints.AssignRoomSectionAsync`
already uses for FLR-06's section assignment. Cash-handling accountability
in the stronger sense — a manager sign-off, a supervised blind count — is
PAY-10/11 territory, not this.

**`GET .../cash-sessions/current` returns `204`, not `200` with a `null`
body, when nothing is open.** `Results.Ok<T>(null)` in ASP.NET Core Minimal
APIs writes a zero-byte body for a `200`, not the JSON literal `"null"` —
so a client calling `response.json()` unconditionally throws a parse error
("Unexpected end of JSON input") rather than receiving a value it can check
for null. Found live via the E2E suite's own first attempt to call this
endpoint, before it ever reached a commit — fixed by returning `204` (the
correct way to say "no body" over HTTP) instead, with clients checking the
status code rather than parsing an empty body. Every downstream client
already had to handle `204` on `undefined` on other mutating endpoints
(API-05's idempotency replay path returns one too), so this needed no new
client-side plumbing beyond that existing check.

**A movement always requires a reason (PAY-09).** The same "never silent"
instinct `Order.VoidLine` already applies to voiding a line — a pay-out
with no explanation defeats the entire point of recording one. Enforced
both by the API layer (`400 cash_movement.reason_required` before the
staff lookup even runs) and by `CashMovement`'s own constructor, since
unlike the one-open-session-per-terminal rule, "does this row have a
non-empty reason" needs no state beyond the row itself.

**A movement can only be recorded against an *open* session.** Checked at
the API layer (`400 cash_movement.session_closed`), not the domain type —
`CashMovement` has no way to know its own session's status without a
query, the same "the check needs to see state beyond one row" reasoning
`CashSession`'s own one-open-session-per-terminal rule already uses.
Recording cash movements against an already-closed till would let a
number silently appear in a session's own history after that session was
supposedly reconciled and done.

**A blind count (PAY-10) is three nullable fields on `CashSession` itself,
not a new table.** A session has at most one count, the same "no entity
where a plain field says the same thing" call `ClosedAtUtc` already made
for closing — a separate `CashCount` table would need its own
one-per-session uniqueness constraint to say nothing a nullable
`CountedAmount` doesn't already say for free. Recorded via
`CashSession.RecordCount`, a `Result`-returning domain method (not a
throwing constructor, matching `Close`'s own shape) — this lets the
endpoint call it directly and translate a `Result.Failure` straight into
`.ToProblem()`, no API-layer pre-validation duplicating the domain's own
rules. A second count on the same session is rejected
(`cash_session.already_counted`) rather than silently overwriting the
first: the entire value of *blind* is that it wasn't influenced by
anything, including an earlier attempt at the same count.

**`Payment.CashSessionId` lives on `Payment` (Payments module), not as a
cross-module reference — because it doesn't need to be one.** `CashSession`
is already in the same module, so this is an ordinary nullable foreign key
with an index, not the opaque-Guid convention `TerminalId`/`OpenedByStaffId`
need for reaching into Identity. It's optional and unvalidated against the
session's own open/closed state deliberately: a payment predates the
concept of "this terminal is mid-shift" for takeaway/online-order flows that
never open a session at all, and backdating one after the fact (e.g. an
order started before a shift's cash session was opened) is a real scenario,
not a bug to prevent. The one thing that *is* validated is that the
`cashSessionId` a payment names actually exists — `404
cash_session.not_found` — the same defensive-against-typos posture every
other opaque-reference field in this codebase takes.

**A split payment (PAY-04) takes one `cashSessionId` for the whole batch,
not one per tender.** A split happens at one terminal at one moment — cash
and card tenders in the same split were physically handled by the same
person at the same drawer, so attributing them to two different sessions
would never be meaningful.

**The variance endpoint recomputes everything on every request instead of
storing a snapshot at close time.** A `CashSession` can accumulate payments
after it closes — nothing gates that, see above — so a stored snapshot
taken at `Close()` could go stale the moment a late payment lands. Computing
fresh from `CashMovement`/`Payment` rows means the report is always
consistent with what's actually in the database, at the cost of a few
extra queries per request — an acceptable trade for a report a manager
checks once per shift, not a hot path.

**Only `PaymentMethod.Cash` payments count toward `totalCashPaymentsTaken`.**
A card tender never touches the physical drawer — including it would make
the "expected amount in the drawer" figure wrong by construction, not just
imprecise.

## Behaviour

1. A restaurant registers a terminal once (IDN-01, `POST /sites/{id}/terminals`)
   — a one-time setup step, not part of this feature.
2. At the start of a shift, a staff member (via `admin`'s "Cash sessions"
   screen today) picks the terminal, enters the float counted into the
   drawer, and picks who's opening it.
3. `POST /cash-sessions` confirms the terminal and staff are both real,
   the float isn't negative, and that terminal has no other open session,
   then persists the row.
4. The terminal's row in `admin` shows "Aberta" (Open) with who opened it
   and the float, for as long as it stays open.
5. At the end of the shift, closing it (`POST /cash-sessions/{id}/close`)
   is a bare status flip — `ClosedAtUtc` is set, `IsOpen` becomes `false`.
   The terminal is then free for a new session to be opened against it.
6. While the session stays open, staff can record a pay-in or pay-out
   (`POST /cash-sessions/{id}/movements`) — a direction, an amount, a
   required reason, and who recorded it. `admin`'s "Cash sessions" screen
   shows a running list under the open session, with an "Add pay-in /
   pay-out" form.
7. Before closing, staff count what's physically in the drawer and record
   it blind (`POST /cash-sessions/{id}/count`) — who counted, and how
   much. `admin` shows a "Record blind count" action next to the movements
   panel while the session is open and uncounted; once recorded, it
   collapses to a read-only line — there's no edit or re-count path.
8. A cash payment recorded anywhere in the system can optionally name the
   cash session it was taken under (`cashSessionId` on `POST
   /orders/{id}/payments` or `.../payments/split`) — the POS UI doesn't
   wire this up yet (see Open questions), so today it's exercised via the
   API directly.
9. Once a count exists, `GET /cash-sessions/{id}/variance` answers "what
   should be in the drawer, and how far off was the count." `admin` shows
   this automatically under the blind-count panel the moment a count is
   recorded, whether or not the session has since been closed.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today, same as every
other mutating endpoint in this codebase before the eventual SYN work.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| `openingFloat` is negative | Rejected before the terminal/staff lookups | `400 cash_session.invalid_opening_float` |
| `terminalId` doesn't exist | No session written | `404 identity.terminal_not_found` |
| `staffId` doesn't exist | No session written | `404 identity.staff_not_found` |
| That terminal already has an open session | Rejected — only one at a time | `409 cash_session.already_open` |
| Closing a session that's already closed | Rejected | `400 cash_session.already_closed` |
| Closing or fetching an unknown session id | — | `404 cash_session.not_found` |
| `GET .../cash-sessions/current` for a real terminal with nothing open | Not an error — a normal, common state between shifts | `204 No Content`, empty body |
| `GET .../cash-sessions/current` for an unknown terminal | — | `404 identity.terminal_not_found` |
| A movement's `direction` isn't `"PayIn"`/`"PayOut"` | Rejected | `400 cash_movement.invalid_direction` |
| A movement's `amount` is zero or negative | Rejected | `400 cash_movement.invalid_amount` |
| A movement's `reason` is missing, empty or whitespace | Rejected before the staff lookup | `400 cash_movement.reason_required` |
| A movement's `staffId` doesn't exist | No movement written | `404 identity.staff_not_found` |
| A movement is recorded against a session that's already closed | Rejected | `400 cash_movement.session_closed` |
| Recording or listing movements against an unknown session id | — | `404 cash_session.not_found` |
| A count's `countedAmount` is negative | Rejected | `400 cash_session.invalid_counted_amount` |
| A count is recorded against a session that's already closed | Rejected | `400 cash_session.already_closed` |
| A second count is recorded against the same session | Rejected — at most once | `400 cash_session.already_counted` |
| A count's `staffId` doesn't exist | No count recorded | `404 identity.staff_not_found` |
| Recording a count against an unknown session id | — | `404 cash_session.not_found` |
| A payment names a `cashSessionId` that doesn't exist | No payment written | `404 cash_session.not_found` |
| Requesting variance for an unknown session id | — | `404 cash_session.not_found` |
| Requesting variance before any count has been recorded | Not an error — `countedAmount`/`variance` are simply `null` | `200`, `expectedAmount` populated, `countedAmount`/`variance` null |

## Data

New `cash_sessions` table in the existing `payments` schema (same
`PaymentsDbContext`, no new module or migration project): `Id`, `TerminalId`
(indexed, unindexed foreign reference), `OpenedByStaffId`, `OpeningFloat`
(mapped via `MapMoney`, the same convention every other `Money` column in
this codebase uses), `OpenedAtUtc`, `ClosedAtUtc` (nullable),
`CountedAmount` (nullable, `MapOptionalMoney` — the same convention
`MenuItem.TakeawayPrice`, CAT-06, already uses for a `Money` that's
genuinely absent rather than zero), `CountedByStaffId` (nullable),
`CountedAtUtc` (nullable). `IsOpen` is a computed property (`ClosedAtUtc is
null`), never a stored column — `CashSessionConfiguration` ignores it, the
same "never store a derived value" rule `Payment.AmountApplied` already
follows. RLS enabled the
standard way (`EnableFor`/`EnableSystemReadFor` in the migration's `Up()`,
hand-added since the EF Core scaffolder never emits these calls — see
[multi-tenancy.md](../architecture/multi-tenancy.md)). Generated via a real
`dotnet ef migrations add`, not hand-written from scratch — the scaffolded
table/column/index shape was correct as generated; only the two RLS calls
needed hand-adding, and even those needed a fix after the first attempt: the
`EnableFor`/`EnableSystemReadFor` extension signature is `(table, schema)`,
not `(schema, table)` — easy to get backwards since the *existing*
`payments.payments` table's own migration call reads identically either way
(`EnableFor("payments", "payments")`, table and schema happening to share a
name), which is exactly what masked the bug on the first attempt here until
a real `dotnet run` against Postgres surfaced `schema "cash_sessions" does
not exist` immediately at startup.

New `cash_movements` table in the same `payments` schema, added the same
session as `cash_sessions` itself (PAY-09): `Id`, `CashSessionId` (indexed,
unindexed foreign reference), `Direction` (string-converted enum,
`PayIn`/`PayOut`), `Amount` (`MapMoney`), `Reason` (`character
varying(300)`, the same length `OrderLine.VoidReason` already uses for a
comparable free-text field), `RecordedByStaffId`, `RecordedAtUtc`. This
migration's own `EnableFor`/`EnableSystemReadFor` calls got the argument
order right the first time — checked against the actual method signature
rather than by analogy to another migration, the fix for the exact trap
`AddCashSession`'s own migration hit.

PAY-10's own migration (`AddCashSessionBlindCount`) added only the three
nullable `Counted*` columns above to the already-existing `cash_sessions`
table — no new table, so no `EnableFor`/`EnableSystemReadFor` calls at all;
RLS was already enabled on that table by `AddCashSession`.

PAY-11's migration (`AddCashSessionIdToPayment`) adds one nullable
`CashSessionId` column plus an index to the existing `payments` table (same
`payments` schema, same `PaymentsDbContext` as `CashSession` itself) —
generated via a real `dotnet ef migrations add`, no RLS calls needed since
`payments` already has RLS enabled. The variance report itself has no
table of its own — `CashSessionVarianceDto` is computed on every request
from `CashSession.OpeningFloat`/`CountedAmount`, a `SUM` over
`CashMovement` split by `Direction`, and a `SUM` over `Payment.AmountApplied`
filtered to `CashSessionId` = the session and `Method` = `Cash`.

## API

- `POST /cash-sessions` — body `{ terminalId, staffId, openingFloat }`,
  returns `201` with a `CashSessionDto`.
- `POST /cash-sessions/{cashSessionId}/close` — no body, returns `200` with
  the updated `CashSessionDto`.
- `GET /cash-sessions/{cashSessionId}` — returns `200` with a
  `CashSessionDto`, or `404 cash_session.not_found`.
- `GET /terminals/{terminalId}/cash-sessions/current` — returns `200` with
  a `CashSessionDto` if one is open, `204` with no body otherwise.
- `POST /cash-sessions/{cashSessionId}/movements` — body `{ direction,
  amount, reason, staffId }`, returns `201` with a `CashMovementDto`.
- `GET /cash-sessions/{cashSessionId}/movements` — returns `200` with every
  `CashMovementDto` recorded against the session, oldest first.
- `POST /cash-sessions/{cashSessionId}/count` — body `{ staffId,
  countedAmount }`, returns `200` with the updated `CashSessionDto`
  (`countedAmount`/`countedByStaffId`/`countedByStaffName`/`countedAtUtc`
  all populated). Rejected with a `400` if the session is closed or
  already counted — see Failure modes.
- `GET /cash-sessions/{cashSessionId}/variance` (PAY-11) — returns `200`
  with a `CashSessionVarianceDto`: `openingFloat`, `totalPayIns`,
  `totalPayOuts`, `totalCashPaymentsTaken`, `expectedAmount` (always
  populated), and `countedAmount`/`variance` (both `null` until a blind
  count exists). `404 cash_session.not_found` for an unknown session id.
  Works whether the session is still open or already closed.
- `POST /orders/{orderId}/payments` and `POST
  /orders/{orderId}/payments/split` (PAY-01/02/03/04) now accept an
  optional `cashSessionId` — see PAY-01's own doc
  ([cash-payments.md](cash-payments.md)) for the full request shape.
  `404 cash_session.not_found` if it's set but doesn't resolve to a real
  session; omitted entirely, a payment carries no session (`cashSessionId:
  null` in the response), same as today.

`CashSessionDto` carries `terminalLabel`/`openedByStaffName`, and
`CashMovementDto` carries `recordedByStaffName`, resolved fresh from
Identity on every response, never snapshotted onto the row — the same
pattern `PaymentDto.attributedStaffName` already uses. Documented in
`docs/openapi/v1.json` and typed in `src/web/sdk/src/schema.ts`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. A cash session records who opened a till and with how much float, a
movement records cash added or removed mid-shift, and a count records what
was physically found in the drawer — none of the three issue, reference,
or correct a fiscal document.

## Permissions

None — any real staff id can open/close a session, record a movement, or
record a count against any terminal, same as every other non-manager-gated
mutation in this codebase today. Real cash-handling accountability (a
supervised blind count sign-off, a variance report someone approves) is
PAY-11 territory, not this.

## Testing

**Backend:** covered indirectly through the endpoints' own validation logic
(terminal/staff lookups, float validation, the one-open-session-per-terminal
check, the movement direction/amount/reason checks, the count
amount/already-counted/session-closed checks, the variance computation) —
no dedicated `Brasa.Api.IntegrationTests` class yet; the full backend suite
(101 tests) and `verify.ps1` (build, tests, OpenAPI drift, breaking-change
check, vulnerable-package scan) all pass with the module wired in. Also
verified by hand against a real Postgres instance before the E2E suite
existed for each slice of this feature — for sessions: open, `GET current`,
a duplicate open rejected with `409`, close, `GET current` returning `204`,
reopen (the same pass that caught the `Results.Ok<T>(null)` trap documented
above); for movements: open a session, record a pay-out, list it, close the
session, confirm a further movement is rejected, confirm the validation
rejections; for counts: open a session, record a count, re-count rejected,
negative amount rejected, count-after-close rejected; for variance
(PAY-11): opened a session with a 50.00 float, recorded a 20.00 pay-in and
a 5.00 pay-out, rang up and paid a 10.00 cash order tagged with the
session, confirmed `expectedAmount` came back as 75.00 and
`countedAmount`/`variance` were both `null`; recorded a blind count of
73.00 and confirmed `variance` came back as -2.00, both before and after
closing the session; confirmed a payment naming an unknown
`cashSessionId` is rejected with `404 cash_session.not_found` and leaves
the order's balance untouched (a following ordinary payment still owed the
full amount).

**`src/web/e2e/tests/cash-sessions.spec.ts`** — 20 tests. PAY-08: opening a
session and confirming `GET current` returns it with the right
terminal/staff/float; a duplicate open on the same terminal rejected with
`cash_session.already_open`; closing a session, confirming `GET current`
now returns `204`/`null`, a second close rejected with
`cash_session.already_closed`, and the terminal accepting a fresh session
once freed; a negative float, an unknown terminal id, and an unknown staff
id each rejected with their own code; `404`s on an unknown session id (both
close and fetch) and an unknown terminal id (`current`); and a real
`admin` UI test opening then closing a session through the real browser.
PAY-09: a pay-out then a pay-in recorded against an open session, listed
oldest-first with names resolved on each; a closed-session/invalid-direction/
non-positive-amount/empty-reason/unknown-staff rejection sweep; `404`s
recording or listing movements against an unknown session id; and a real
`admin` UI test recording a pay-out through the movements panel, confirming
the reason and amount appear in the list. PAY-10: recording a count and
confirming both the response and a follow-up `GET` reflect it with the
counting staff member's name resolved; a four-way rejection sweep (a
second count, a negative amount, an unknown staff id, a count on a closed
session — the last two against fresh sessions/terminals so they don't
collide with the intentional first count); a `404` recording a count
against an unknown session id; and a real `admin` UI test recording a
count through the count panel and confirming it collapses to a read-only
line with no further "record count" trigger visible. Every test creates
its own fresh terminal (`createTerminal`) rather than sharing the seeded
demo "Caixa 1," the same "isolated resource, not the shared seeded one"
instinct `staff-login.spec.ts` already uses for its own locked-out staff
member — necessary here since only one session can be open per terminal at
a time and E2E workers run in parallel. PAY-11 (6 of the 20): computes
`expectedAmount` from a float plus a pay-in, a pay-out, and a real cash
payment against a real order, confirming `countedAmount`/`variance` are
both `null` before a count exists and correctly populated (including a
negative variance) after one, and still computable after the session
closes; a card tender is confirmed to leave `totalCashPaymentsTaken` at
zero; a split payment's cash tender is confirmed to count while its card
tender doesn't, using the batch-level `cashSessionId`; a payment against an
unknown `cashSessionId` is rejected with `404 cash_session.not_found` and
leaves the order's balance untouched; the variance endpoint itself `404`s
for an unknown session id; and a real `admin` UI test records a blind count
through the count panel and confirms the variance report appears
immediately after with the correctly formatted expected amount and
variance badge.

## Open questions

- **No `pos` UI for any of PAY-08/09/10/11.** `admin`'s back-office screen
  is the only place a session can be opened, closed, counted, or have its
  variance viewed today, and the POS payment screen doesn't offer a
  `cashSessionId` to attach to a tender — a terminal opening its own shift
  session and having its own payments auto-attributed to it is the more
  realistic real-world flow, not built yet.
- **`cashSessionId` on a payment is optional and never auto-filled.**
  Nothing infers "this terminal has an open session, attribute cash
  payments to it automatically" — a caller must know and pass the session
  id explicitly. Until the POS UI does that wiring, every cash payment
  taken through the POS today contributes nothing to any session's
  variance report, silently — not rejected, just not counted. Worth
  revisiting once PAY-11 has a real UI consumer beyond variance display.
- **Closing a session doesn't require or even show a count first.** A
  manager can close a till with money still uncounted, or with a variance
  never reviewed — `Close()` and `RecordCount()` remain entirely
  independent operations. Whether closing should eventually require a
  count, or surface the variance as a confirmation step, is a real product
  decision, not a technical gap.
- **No history view.** There's no "list every past session for this
  terminal" endpoint — only the currently-open one, and the variance
  endpoint requires already knowing the session's id. A per-terminal or
  per-site shift history is real future work.
- **No permissions gate on the variance report.** Same as every other
  cash-session operation today — any real staff id (or, for the variance
  `GET`, no staff id check at all) can view it. Real accountability (a
  manager-only variance review, a sign-off) is future work.
