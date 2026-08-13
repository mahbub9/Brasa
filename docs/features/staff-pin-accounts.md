# Staff PIN accounts

> **Status:** 🚧 in progress — PIN hashing, lockout and rotation are built and verified live; "sign in on a paired terminal" (this row's own IDN-08 title) is not, since no terminal pairing exists yet
> **Module:** Identity
> **Roadmap:** I3 (pulled forward)

## What it is

A staff member — waiter, kitchen, manager — has a name, a role (Staff or
Manager), and a 4–6 digit PIN. `admin` can add staff, see who's currently
locked out, and reset a PIN. The verification mechanism itself (`POST
/staff/{id}/verify-pin`) is real and live, with a genuine lockout policy
after repeated wrong guesses — but nothing in either web client actually
calls it to sign anyone in yet.

## Why it works this way

**Verifies a *known* staff id's PIN, not "identify me by PIN alone with no
picker."** A real PIN pad at a terminal could work either way: staff tap
their own name tile first then enter a PIN, or they type a PIN blind and
the system figures out who they are by trying it against everyone at that
site. The second design was deliberately not chosen here. Verifying a PIN
against a specific staff id keeps lockout tracking simple and correct —
each failed attempt increments *that* staff member's own counter. Trying
one PIN against every staff member at a site to "identify" someone would
mean a correct PIN for Ana looks like a wrong guess for every other staff
member the system also tried it against along the way, incorrectly
penalising people who never attempted anything. Scoped verification avoids
that entirely, at the cost of needing a picker UI (tap your name, then
type your PIN) rather than a blind PIN pad — the same pattern several real
POS systems already use.

**PBKDF2, not a dependency.** `Rfc2898DeriveBytes` is already in .NET —
210,000 iterations (OWASP's 2023 minimum for PBKDF2-HMAC-SHA256), stored
alongside each hash so a future increase never invalidates an
already-issued PIN, verified with a constant-time comparison
(`CryptographicOperations.FixedTimeEquals`). The same "hand-roll it only
where the standard library already gives a safe building block" instinct
this codebase applied to CSV parsing (CAT-17) and the event dispatcher
(ADR 0006) rather than pulling in a package for either.

**Locks out after 5 consecutive incorrect PINs, for 15 minutes — and
refuses even the *correct* PIN while locked.** A shared terminal in a busy
kitchen is exactly the kind of place someone might try a few PINs at
random; the lockout exists to make that expensive, not to punish a
genuine staff member who mistypes once or twice. Resetting the PIN
(`PUT /staff/{id}/pin`) clears the lockout together with the PIN itself —
a fresh start, not a way to talk your way out of a lockout mid-window with
the same compromised PIN.

**PIN rotation is an admin action, not self-service.** There is no old-PIN
check on `PUT /staff/{id}/pin` — an admin (today: anyone, since no
authorization gate exists at all) can reset any staff member's PIN
outright. Same "ships ahead of manager authorisation" shape every other
admin mutation in this codebase already has; IDN-11 is the eventual real
gate.

**Not wired into any endpoint's own authorization decision.** ORD-10
(void) and ORD-11 (discount) both still have "no manager-authorisation
gate yet" as their own named, deferred gap. This feature ships the
verification *mechanism*, ready for whichever future task (IDN-11) makes
a privileged action actually require a manager's PIN — it does not,
itself, gate anything yet.

## Behaviour

1. An admin creates a staff member: `POST /sites/{siteId}/staff` with
   `{ name, role, pin }`. `role` is `"Staff"` or `"Manager"`.
2. Anyone with a staff id verifies a PIN: `POST /staff/{id}/verify-pin`
   with `{ pin }`. A match resets that staff member's failure counter; a
   miss increments it, locking the account for 15 minutes on the 5th
   consecutive miss.
3. `GET /sites/{siteId}/staff` lists every staff member at a site,
   including whether each is currently locked (`isLocked`, computed
   server-side from `LockedUntilUtc` against the caller's own injected
   clock — a client never compares a raw timestamp against its own).
4. An admin resets a PIN: `PUT /staff/{id}/pin` with `{ pin }` — clears
   any lockout at the same time.
5. `admin`'s "Equipa" screen wraps steps 1, 3 and 4 in a real UI: add a
   staff member, see the list with role/lock badges, reset a PIN inline.

## Offline behaviour

Not applicable — cloud API endpoints, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| `name` missing/empty on create | Rejected | `400 identity.invalid_staff_name` |
| `role` not `"Staff"` or `"Manager"` | Rejected | `400 identity.invalid_staff_role` |
| `pin` not 4–6 digits, on create or reset | Rejected | `400 identity.invalid_pin` |
| Create targets an unknown site | Rejected | `404 identity.site_not_found` |
| Verify/reset targets an unknown staff id | Rejected | `404 identity.staff_not_found` |
| PIN doesn't match | Rejected, `FailedPinAttempts` incremented (possibly triggering a lockout) | `400 identity.pin_incorrect` |
| Verify attempted while locked out (even with the correct PIN) | Rejected | `409 identity.staff_locked` |

## Data

`Staff` (Identity module, `SiteId` a direct reference — same tier
`Terminal` already sits at) — `Name`, `Role` (`StaffRole`: Staff/Manager),
`PinHash` (private, PBKDF2-encoded, never exposed on any DTO),
`FailedPinAttempts`, `LockedUntilUtc`. Mapped via EF's string-keyed
`Property<string>("PinHash")`, which resolves a genuinely `private`
auto-property by reflection — no shadow property, no public getter
anywhere reads the raw hash back out except `Staff`'s own domain methods.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/sites/{siteId}/staff` | Create a staff member with an initial PIN |
| `GET` | `/sites/{siteId}/staff` | List every staff member at a site |
| `POST` | `/staff/{staffId}/verify-pin` | Verify a PIN; tracks failures and lockout |
| `PUT` | `/staff/{staffId}/pin` | Reset a staff member's PIN, clearing any lockout |

All mutations take `Idempotency-Key` like every other endpoint in this
API. `POST /staff/{id}/verify-pin` always persists its outcome — even a
*rejected* attempt must save the incremented failure count (and possible
lockout), not just a successful one.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None directly. Once IDN-11 (manager authorisation) wires this into voids
and discounts, *who* authorised a correction becomes part of the audit
trail those actions already keep — but that wiring doesn't exist yet.

## Permissions

None enforced — the entire notion of "who is allowed to do this" doesn't
exist anywhere in this codebase yet (`DevTenantMiddleware` is the whole
auth story until I3). This feature is infrastructure for that future
state, not an example of it: anyone who can reach the API today can
create staff, verify any PIN, or reset any PIN.

## Testing

`staff.spec.ts` — a correct PIN verifies; an incorrect one is rejected
(`identity.pin_incorrect`); 5 consecutive failures lock the account so
even the *correct* PIN is then refused (`identity.staff_locked`);
resetting the PIN clears the lockout and the new PIN works immediately;
an empty name, a malformed PIN (too short/long/non-digit), an
unrecognised role and unknown site/staff ids are all rejected with their
own codes. `admin`'s staff screen adds a staff member and resets their
PIN through the real UI, both proven to actually take effect via a
follow-up API call afterward, not just "the UI showed no error."

## Open questions

- Terminal-scoped sign-in (IDN-07 pairing) doesn't exist — this row's own
  IDN-08 title ("on a paired terminal") names a piece that's genuinely
  not built.
- No blind "identify me by PIN alone" flow — see "Why it works this way"
  for why that was a deliberate choice, not an oversight.
- No delete/deactivate for a staff member — the same narrow-slice shape
  IDN-01 itself established (create + list only) for Organization/Site/
  Terminal.
- Roles beyond a bare Staff/Manager tag (IDN-10's real permissions model)
  don't exist — `StaffRole` is enough to prove the mechanism, not a
  finished authorization system.
- No admin UI exists to pick a site — this screen (and every other
  screen in `admin`) assumes the first organization's first site.
