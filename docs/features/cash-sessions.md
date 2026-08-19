# Cash sessions — *abertura de caixa* with a starting float

> **Status:** ✅ built (mechanism only — see Open questions)
> **Module:** Payments, composed with Identity at the API layer
> **Roadmap:** I6 (`PAY-08`)

## What it is

A staff member declares a starting cash float against a specific terminal
at the start of a shift — *abertura de caixa*, "opening the till." A new
`CashSession` record (Payments module) captures who opened it, which
terminal, how much cash was in the drawer to start, and when. Only one
session can be open per terminal at a time. `POST /cash-sessions` opens
one; `POST /cash-sessions/{id}/close` closes it; `GET
/terminals/{id}/cash-sessions/current` answers "is this terminal's till
currently open, and by whom."

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

## Data

New `cash_sessions` table in the existing `payments` schema (same
`PaymentsDbContext`, no new module or migration project): `Id`, `TerminalId`
(indexed, unindexed foreign reference), `OpenedByStaffId`, `OpeningFloat`
(mapped via `MapMoney`, the same convention every other `Money` column in
this codebase uses), `OpenedAtUtc`, `ClosedAtUtc` (nullable). `IsOpen` is a
computed property (`ClosedAtUtc is null`), never a stored column —
`CashSessionConfiguration` ignores it, the same "never store a derived
value" rule `Payment.AmountApplied` already follows. RLS enabled the
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

## API

- `POST /cash-sessions` — body `{ terminalId, staffId, openingFloat }`,
  returns `201` with a `CashSessionDto`.
- `POST /cash-sessions/{cashSessionId}/close` — no body, returns `200` with
  the updated `CashSessionDto`.
- `GET /cash-sessions/{cashSessionId}` — returns `200` with a
  `CashSessionDto`, or `404 cash_session.not_found`.
- `GET /terminals/{terminalId}/cash-sessions/current` — returns `200` with
  a `CashSessionDto` if one is open, `204` with no body otherwise.

`CashSessionDto` carries `terminalLabel`/`openedByStaffName` resolved fresh
from Identity on every response, never snapshotted onto the row — the same
pattern `PaymentDto.attributedStaffName` already uses. Documented in
`docs/openapi/v1.json` and typed in `src/web/sdk/src/schema.ts`.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. A cash session records who opened a till and with how much float —
it never issues, references, or corrects a fiscal document.

## Permissions

None — any real staff id can open or close a session against any terminal,
same as every other non-manager-gated mutation in this codebase today.
Real cash-handling accountability (a supervised blind count, a variance
report someone signs off on) is PAY-10/11, not this.

## Testing

**Backend:** covered indirectly through the endpoints' own validation logic
(terminal/staff lookups, float validation, the one-open-session-per-terminal
check) — no dedicated `Brasa.Api.IntegrationTests` class yet; the full
backend suite (101 tests) and `verify.ps1` (build, tests, OpenAPI drift,
breaking-change check, vulnerable-package scan) all pass with the module
wired in. Also verified by hand against a real Postgres instance before the
E2E suite existed for this feature — open, `GET current`, a duplicate open
rejected with `409`, close, `GET current` returning `204`, reopen — the same
pass that caught the `Results.Ok<T>(null)` trap documented above.

**`src/web/e2e/tests/cash-sessions.spec.ts`** — 6 tests: opening a session
and confirming `GET current` returns it with the right terminal/staff/float;
a duplicate open on the same terminal rejected with
`cash_session.already_open`; closing a session, confirming `GET current`
now returns `204`/`null`, a second close rejected with
`cash_session.already_closed`, and the terminal accepting a fresh session
once freed; a negative float, an unknown terminal id, and an unknown staff
id each rejected with their own code; `404`s on an unknown session id (both
close and fetch) and an unknown terminal id (`current`); and a real
`admin` UI test opening then closing a session through the real browser,
confirming the row's badge and detail text update both times. Every test
creates its own fresh terminal (`createTerminal`) rather than sharing the
seeded demo "Caixa 1," the same "isolated resource, not the shared seeded
one" instinct `staff-login.spec.ts` already uses for its own locked-out
staff member — necessary here since only one session can be open per
terminal at a time and E2E workers run in parallel.

## Open questions

- **Nothing consumes an open session yet.** No payment endpoint checks
  whether a session is open, and closing one doesn't reconcile it against
  anything. This is deliberate (see "Why it works this way" above) but
  means the feature today is purely a bookkeeping record with no enforced
  effect — PAY-10 (blind count) and PAY-11 (*fecho de caixa* with variance
  reporting) are what give it real teeth.
- **No `pos` UI.** `admin`'s back-office screen is the only place a session
  can be opened or closed today. A terminal opening its own shift session
  from the POS itself — the more realistic real-world flow — is a real
  future trigger, not built yet.
- **No history view.** There's no "list every past session for this
  terminal" endpoint — only the currently-open one. Revisit once PAY-11's
  reporting needs it.
