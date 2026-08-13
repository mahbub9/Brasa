# Manager authorisation for voids and discounts

> **Status:** ✅ done — the gate itself, not a UI for it. See "Open questions."
> **Module:** Identity (the check) + Ordering (the two endpoints it guards)
> **Roadmap:** I3 (pulled forward)

## What it is

Voiding a line and applying a discount both give away revenue that was
already rung up — exactly the kind of action a real POS restricts to a
manager. `POST /orders/{id}/lines/{lineId}/void`,
`PUT /orders/{id}/lines/{lineId}/discount` and `PUT /orders/{id}/discount`
now all require a manager's own staff id and PIN in the request body,
verified server-side before the void or discount is ever attempted. Both
endpoints named this as their own deferred gap when they shipped
(ORD-10/ORD-11); [Staff PIN accounts](staff-pin-accounts.md) (IDN-08/09) is
what finally made a real manager credential possible to check.

## Why it works this way

**Role is checked before the PIN, not after.** A request naming a real
staff id whose role is `Staff`, not `Manager`, is rejected outright — the
wrong *kind* of credential, never even reaching `Staff.VerifyPin`. This
matters for the same reason IDN-08/09 designed lockout the way it did: if a
non-manager's PIN were tried and failed, it would consume that person's own
lockout budget for an action they were never authorised to take part in.
Checking role first means a waiter's PIN typed into a manager prompt by
mistake costs them nothing.

**Reuses `Staff.VerifyPin` exactly, not a parallel mechanism.** Once role is
confirmed, the PIN is checked the identical way
`POST /staff/{id}/verify-pin` already checks one — same PBKDF2 comparison,
same 5-strike/15-minute lockout, same "persist the identity `DbContext`
regardless of outcome" rule, because a wrong guess here must still advance
that manager's own `FailedPinAttempts` just as it would anywhere else. Two
different lockout policies for the same underlying credential would be a
bug waiting to happen, not a feature.

**A per-call credential, not a session.** Nothing about a verified manager
is cached, remembered, or reused across requests. Every void and every
discount re-proves it from scratch. This is the same "known id, not blind
PIN entry, no session concept yet" shape IDN-08/09 already established —
there is no terminal pairing (IDN-07) or OAuth/JWT session (IDN-03…05) for
a verified credential to attach to yet, so a session is not a corner that
was cut, it is infrastructure that does not exist yet.

**Composes Identity into Ordering at the API layer, not a module
reference.** `OrderEndpoints.AuthorizeManagerAsync` injects
`IdentityDbContext` directly into `Brasa.Api`'s own endpoint handler — the
same shape `PriceListEndpoints` already uses to confirm a `siteId` is real
(CAT-05). Ordering's own domain methods (`Order.VoidLine`,
`SetLineDiscount`, `SetDiscount`) were not touched at all: they have no
notion of "who is asking," only whether the void or discount itself is
well-formed. Authorisation is layered on entirely by the caller, per
[module-boundaries.md](../architecture/module-boundaries.md) rule 5.

## Behaviour

1. A client attempting to void a line or set a discount includes
   `managerStaffId` and `managerPin` in the request body, alongside that
   endpoint's own existing fields (`reason` for a void; `type`/`value` for
   a discount).
2. The server resolves `managerStaffId` against `Staff`. Unknown id → 404.
   A real id whose `role` isn't `Manager` → 403, no PIN check at all.
3. A real manager's PIN is verified via the same mechanism
   `POST /staff/{id}/verify-pin` uses — wrong PIN → 400, advancing that
   manager's own failure count; correct PIN → resets it; either way, the
   attempt is persisted immediately, not deferred until the whole request
   succeeds.
4. Only once authorisation succeeds does the underlying void or discount
   run, exactly as it did before this gate existed.

## Offline behaviour

Not applicable — cloud API endpoints, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| `managerStaffId` names no real staff member | Rejected before any PIN check | `404 identity.staff_not_found` |
| `managerStaffId` names a real staff member whose role isn't `Manager` | Rejected, PIN never checked, no lockout impact | `403 identity.staff_not_manager` |
| `managerPin` doesn't match the named manager's PIN | Rejected, that manager's `FailedPinAttempts` advances | `400 identity.pin_incorrect` |
| 5th consecutive wrong PIN against the same manager | Rejected, that manager locked for 15 minutes | `409 identity.staff_locked` — even a correct PIN is refused until the lockout clears or an admin resets it |
| Authorisation succeeds but the void/discount itself is invalid (e.g. a blank void reason, an out-of-range discount) | Rejected by the existing, unchanged domain validation | Same codes ORD-10/ORD-11 already had (`order.void_reason_required`, `order.invalid_discount`, etc.) |

## Data

No new persisted state. `Staff.FailedPinAttempts`/`LockedUntilUtc` (IDN-09)
are the only fields this touches, and only because a wrong manager PIN here
is, from `Staff`'s own point of view, indistinguishable from a wrong PIN
anywhere else.

## API

| Method | Route | New fields |
|---|---|---|
| `POST` | `/orders/{orderId}/lines/{lineId}/void` | `managerStaffId`, `managerPin` (alongside the existing `reason`) |
| `PUT` | `/orders/{orderId}/lines/{lineId}/discount` | `managerStaffId`, `managerPin` (alongside the existing `type`/`value`) |
| `PUT` | `/orders/{orderId}/discount` | `managerStaffId`, `managerPin` (alongside the existing `type`/`value`) |

No new routes — this is a body-shape addition to three already-shipped
endpoints. Both fields are effectively required: an omitted
`managerStaffId` defaults to an all-zero GUID, which resolves to no real
staff member and 404s the same as any other unknown id.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None directly, and unlike a void or a discount's own effect on
`FiscalDocumentLine`, this gate itself never reaches the fiscal layer — it
either lets the existing void/discount logic run unchanged or stops before
it. *Who* authorised a correction is not yet recorded anywhere a report
could surface it (see "Open questions").

## Permissions

This *is* the permissions model, narrowly: "a real `Staff` row with
`Role == Manager` and a correct PIN" is the entire authorisation check that
exists anywhere in this codebase today. There is no broader roles-and-
permissions system (IDN-10) behind it — `StaffRole` is a bare two-value tag,
not a claims or scopes system.

## Testing

`manager-authorization.spec.ts` — a non-manager credential is rejected
without the line being touched, and the real manager then succeeds right
after; an unknown `managerStaffId` and a manager's own wrong PIN are both
rejected, then the correct PIN still works; the same gate covers line and
order discounts, and role is checked before PIN (a non-manager's wrong PIN
still surfaces as "not a manager," never "incorrect PIN"); 5 consecutive
wrong PINs against a freshly-created, isolated manager lock them out even
for their own correct PIN afterward — deliberately not the shared seeded
demo manager, so parallel specs that depend on her staying usable
(`void-line.spec.ts`, `discounts.spec.ts`, `order-line-quantity.spec.ts`,
`split-by-item.spec.ts`) are unaffected. Those four specs needed no
test-logic changes at all: `support/api.ts`'s helpers default every
void/discount call to the seeded demo manager's credentials unless a caller
passes its own, so they keep testing exactly what they always tested.

## Open questions

- No pos/admin UI prompts for a manager credential yet — neither client has
  a staff-picker at all, so this ships the gate itself, verified only at
  the API level, the same "mechanism before the trigger" shape ORD-10/11
  themselves shipped in.
- No audit trail records *which* manager authorised a given void or
  discount — the credential is verified and then discarded, not stored
  anywhere the action's own record could reference back to it. A future
  pass would need to decide where that belongs (the order line itself? a
  separate audit table?) rather than bolting it on as an afterthought.
- A locked-out manager has no faster recovery path than an admin's
  `PUT /staff/{id}/pin` reset or waiting out the 15-minute window — same as
  IDN-09 itself.
- `IDN-10` (a real roles-and-permissions model) doesn't exist — this reads
  a bare `StaffRole` enum, not a claims or scopes system.
