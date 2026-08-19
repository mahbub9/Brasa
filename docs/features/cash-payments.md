# Cash and card payments — a tender recorded against an order's remaining balance

> **Status:** ✅ done — cash and card, partial tenders, an atomic split
> across methods, an optional tip attributed to staff, and an optional cash
> session attribution consumed by *fecho de caixa* variance reporting
> (PAY-11) — see [cash-sessions.md](cash-sessions.md). MB WAY/Multibanco
> (PAY-13) and an integrated TPA (PAY-14) are both deferred past MVP — see
> `docs/product/backlog.md`.
> **Module:** Payments (new module this task), composed with Ordering (and,
> for tip attribution, Identity) at the API layer
> **Roadmap:** I3/I4/I6 (`PAY-01`, `PAY-02`, `PAY-03`, `PAY-04`, `PAY-05`,
> `PAY-06`, `PAY-11`)

## What it is

Real service closes an order (issues the fiscal document) without ever
recording how the guest actually paid — nothing in the codebase before this
tracked a tender or computed change. `Payment` is a new record, in a new
`Brasa.Modules.Payments` module, that captures one tender (cash or card)
against one order's remaining balance: what was still owed, what was handed
over, and the change owed back. `POST /orders/{orderId}/payments` records it;
`GET /orders/{orderId}/payments` lists every payment recorded against an
order. An order can be settled in one tender or several — a tender smaller
than what's owed is a valid partial payment, and the balance tracks across
however many it takes to reach zero. A tip can ride along on any tender,
entirely separate from the balance, optionally credited to a staff member.

## Why it works this way

**A new module, not a field on `Order`.** Payments is a distinct future
concern (methods, sessions, reconciliation, refunds) that has nothing to do
with how an order's lines and totals are computed — the same reasoning that
already keeps Ordering, Catalog, Floor and Fiscal apart. `Payment.OrderId` is
a plain opaque `Guid` reference, the same convention `Order.TableId` already
uses for a Floor `Table`: Payments never queries Ordering directly (see
[module-boundaries.md](../architecture/module-boundaries.md) rule 4).
`PaymentEndpoints` composes `PaymentsDbContext` and `OrderingDbContext` at the
API layer instead — the same shape `PriceListEndpoints` already uses for
Catalog+Identity.

**Purely additive — does not gate `Order.Close()`.** Real service and a
recorded payment both close over the same "guest paid" moment in practice,
but wiring a payment requirement into the already-proven close path would
touch the dozens of existing E2E specs that close an order without ever
recording one. This is the same "mechanism before the trigger" shape this
codebase already uses elsewhere (`TaxRule` not wired into `AddLine`, price
lists not resolved through it, DAT-07's `brasa_system` role with no real
consumer yet). This ships the record; requiring one before close — or
blocking close without one — is a deliberately separate, later decision.
A payment can be recorded before or after `Close()`; the endpoint doesn't
care which, and `payments.spec.ts`'s own test proves the "after" case
explicitly.

**`AmountDue` is a remaining balance, not always the order's full total
(PAY-05).** `RecordPaymentAsync` sums every prior payment's own
`AmountApplied` for the order and subtracts that from `order.Total` before
constructing the new `Payment` — so a second or third tender only needs to
cover what's left, never the whole order again. `Payment.AmountApplied` is
the smaller of what was tendered and what was due, `Change` is whatever's
left over (`AmountTendered - AmountApplied`, always zero unless this tender
overpaid), and `RemainingBalance` is `AmountDue - AmountApplied` — zero once
the order is fully settled. A tender for less than what's owed is not an
error: it's a partial payment, recorded exactly as-is, with `RemainingBalance`
staying positive. Once the balance reaches zero, a further payment is
rejected with `payment.already_settled` rather than silently accepted —
closing the "nothing stops a double payment" gap this page originally left
open as a known trade-off.

**Splitting one settlement across several *methods* as one atomic action
(PAY-04).** Every `Payment` row is still exactly one method — PAY-05's
sequential tenders already let a guest pay cash-then-card across two
separate requests, but nothing tied those two requests together, so a
partial failure between them (say, the network drops after the cash leg
lands but before the card leg is even sent) could leave a confusing
half-settled state with no clean way to tell "this is an interrupted split"
from "the guest genuinely just made a partial cash payment." `POST
/orders/{id}/payments/split` fixes that: one request, a list of tenders
(any mix of `Cash`/`Card`), applied in order against what's left after the
ones before it in the same batch, and if any tender is rejected, *nothing*
in the batch is persisted — atomic by construction (nothing is added to
`PaymentsDbContext` until every tender validates, so the endpoint's single
`SaveChangesAsync` call, already one implicit EF Core transaction, either
writes every row or none), not an explicit `BeginTransactionAsync`. A
shared `BuildTenderAsync` helper, extracted from the single-tender
endpoint's own validation, is the one place both endpoints apply the
method/amount/tip/card-no-overpay/staff-attribution rules now, so a future
change to one can't silently drift from the other. Deliberately no tip or
staff-attribution fields on a split — crediting a tip to one portion of a
split settlement is ambiguous ("which tender actually earned it?"), so a
tip stays a single-tender-only concept (PAY-06); attribute one on an
ordinary tender instead if a split guest also tips.

**A card tender can never overpay (PAY-03).** `Card` is manually captured
from a standalone TPA — this codebase never talks to a card processor, staff
key in the amount the real terminal already charged, the same "record what
happened, don't drive the hardware" shape `Cash` already had. A TPA has no
change mechanism, so tendering more than the balance by card is rejected
(`400 payment.card_tender_exceeds_balance`) rather than silently producing a
`change` value nobody could actually hand back. Tendering less is still a
valid partial payment, identical to cash. The guard is enforced twice, on
purpose: `Payment`'s own constructor (the domain invariant) and mirrored in
`RecordPaymentAsync` before ever constructing one, so the API returns a
proper `Result`/`Error` (hard rule 5) instead of letting the domain
`ArgumentException` bubble as an unhandled fault.

**A tip is separate from the bill, not its own row (PAY-06).** `TipAmount`
rides along on the same `Payment` as its tender rather than getting a table
of its own — never affects `AmountDue`/`AmountApplied`/`RemainingBalance`.
Attribution (`AttributedStaffId`, a plain opaque reference to Identity's
`Staff`, the same convention `Room.AssignedStaffId` uses) is optional even
when a tip is given — an unattributed tip goes to a shared pool — but a
*named* staff id must resolve to a real `Staff` (`404
identity.staff_not_found`) before the payment is ever constructed, the same
"confirm before constructing" shape `FloorEndpoints.AssignRoomSectionAsync`
already uses for FLR-06's section assignment. `pos` has no staff picker on
the receipt screen, so attribution is automatic: whoever is signed in
(WEB-07) when the tip is recorded.

**The server reads the amount due — the client never sends one.** The
request body carries `method`, `amountTendered`, and optionally `tipAmount`/
`staffId`/`cashSessionId` (PAY-11); `RecordPaymentAsync` loads the order and
every prior payment itself and computes the remaining balance server-side. A
client-sent due amount would let a stale or manipulated screen record a
payment against the wrong balance.

**`cashSessionId` is optional, unvalidated against the session's own
open/closed state, and never inferred automatically.** See
[cash-sessions.md](cash-sessions.md#why-it-works-this-way) for the full
reasoning — in short, a payment predates the concept of a cash session for
flows that never open one, and a caller must know and pass the id
explicitly since nothing here infers "this terminal has an open session."
The one thing that is checked is that the id, if given, resolves to a real
session (`404 cash_session.not_found`).

**`Money.ToString()`, not culture-formatted, in error messages.** An earlier
draft embedded amounts via `Money.Format(CultureInfo.InvariantCulture)` in
the (since-removed) `payment.insufficient_tender` message and rendered a bare
"¤" currency symbol with no code — worse than the plain invariant diagnostic
form (`"300 EUR"`) for an API error string nobody localizes. The convention
carried forward to every message this endpoint builds.

## Behaviour

1. Staff closes an order (or not — see above) and reaches the receipt
   screen, which now always shows a payment panel with the amount currently
   due and a Cash/Card method selector (defaulting to Cash).
2. Staff picks a method, enters what was tendered (and, optionally, a tip),
   and submits.
3. The API computes the order's own remaining balance server-side, validates
   the method and the amount (rejecting a card tender that would overpay),
   and persists a `Payment`, returning the computed `change`,
   `remainingBalance`, `tipAmount`, and — if attributed — the credited
   staff member's name.
4. If `remainingBalance` is still positive, the receipt screen's form stays
   open — showing the updated balance and a running list of tenders already
   recorded, each labelled with its method — so staff can record another
   one, in the same or a different method.
5. Once a tender brings the balance to zero, the form is replaced with a
   confirmation showing the change due from whichever tender settled it
   (always zero for a card tender) and any tip just recorded.
   There is no edit or void path for a recorded payment yet.
6. Staff can tap "Split between cash and card" (PAY-04) instead — two linked
   amount fields replace the single-tender form; typing one auto-fills the
   other with whatever's left of the balance, and one submit records both
   as a single atomic action. No tip field here.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today, same as every
other mutating endpoint in this codebase before the eventual SYN work.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Tendered amount is zero or negative | Rejected before the order is even loaded | `400 payment.invalid_amount_tendered` |
| Method is neither `Cash` nor `Card` | Rejected | `400 payment.unsupported_method` |
| A card tender would exceed the remaining balance | Rejected — a TPA has no change to give back (PAY-03) | `400 payment.card_tender_exceeds_balance` |
| Tip amount is negative | Rejected before the order is even loaded | `400 payment.invalid_tip_amount` |
| A tip is attributed to an unknown staff id | Rejected before any row is written | `404 identity.staff_not_found` |
| Order id doesn't exist | No payment written | `404 order.not_found` (both on record and on list) |
| A cash tender is smaller than the remaining balance | Not an error — recorded as a partial payment, `remainingBalance` stays positive | `201` with `change: 0` and a positive `remainingBalance` |
| A payment is recorded against an order whose balance is already zero | Rejected before any row is written | `400 payment.already_settled` |
| A split's `tenders` array is missing or empty | Rejected before the order is even loaded | `400 payment.empty_split` |
| One tender partway through a split batch fails any of the above checks | The whole batch is rejected — nothing in it is persisted, including otherwise-valid tenders earlier in the same batch | Whichever error the failing tender itself would have produced standalone (e.g. `payment.card_tender_exceeds_balance`) |
| Two terminals record a payment for the same order at once | Both succeed independently — `Payment` carries no concurrency token, and each reads "prior payments" from its own snapshot in time | Both tenders persist; if both read the balance before either wrote, the order can be over-settled (e.g. two terminals each independently see the full balance still owed). A known, accepted gap — see Open questions |
| A payment names a `cashSessionId` that doesn't resolve to a real cash session | No payment written | `404 cash_session.not_found` |

## Data

New `payments` schema, one table (`payments.payments`): `Id`, `OrderId`
(indexed, unindexed foreign reference — see module-boundaries note above),
`Method` (int enum, stored as a string), `AmountDue`/`AmountTendered`/
`TipAmount` (each mapped via `MapMoney`, the same convention every other
`Money` column in this codebase uses), `AttributedStaffId` (nullable, a
plain opaque reference to Identity's `Staff`), `PaidAtUtc`, `CashSessionId`
(nullable, indexed — PAY-11, an ordinary foreign key rather than an opaque
reference since `CashSession` lives in this same module; see
[cash-sessions.md](cash-sessions.md#data)).
`AmountApplied`/`Change`/`RemainingBalance` are all computed properties, not
columns — `PaymentConfiguration` ignores all three, the same "never store a
derived total" rule `Order.Total` follows. Adding `Card` to `PaymentMethod`
needed no migration at all — `Method` was already a plain
`character varying(20)` string column with room to spare. RLS is enabled the
standard way (`EnableFor`/`EnableSystemReadFor` in the migration's `Up()`,
hand-added since the EF Core scaffolder never emits these calls — see
[multi-tenancy.md](../architecture/multi-tenancy.md)).

## API

- `POST /orders/{orderId}/payments` — body `{ method, amountTendered,
  tipAmount?, staffId?, cashSessionId? }` (`tipAmount` defaults to zero,
  `staffId` is optional even when a tip is given, `cashSessionId` is
  PAY-11's optional cash-session attribution), returns `201` with a
  `PaymentDto` (`amountDue`, `amountTendered`, `amountApplied`, `change`,
  `remainingBalance`, `tipAmount` as `MoneyDto`; `attributedStaffId`/
  `attributedStaffName`; `cashSessionId`, `null` unless given).
- `POST /orders/{orderId}/payments/split` (PAY-04) — body `{ tenders: [{
  method, amountTendered }, ...], cashSessionId? }`, no tip/staff fields.
  `cashSessionId` (PAY-11) applies to every tender in the batch, not
  per-tender — a split happens at one terminal at one moment. Returns `201`
  with a `PaymentDto[]`, one per tender, in the order applied. Atomic: if
  any tender in the array is rejected, the response is that tender's own
  error and nothing in the batch is persisted.
- `GET /orders/{orderId}/payments` — returns every payment recorded against
  the order, oldest first (sorted client-side in the endpoint — SQLite,
  [ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md),
  can't translate `ORDER BY` over `DateTimeOffset`, the same limitation
  `TaxRuleEndpoints.GetTaxRulesAsync` already works around). Attributed staff
  names are batch-resolved from Identity in one query per call, never N+1 —
  the same shape `FloorEndpoints.ResolveStaffNamesAsync` uses for
  `RoomDto.AssignedStaffName`.

Both documented in `docs/openapi/v1.json` and typed in
`src/web/sdk/src/schema.ts` (regenerated across PAY-03/04/05/06; `method`
on the single-tender endpoint was always a plain `string` in the wire
schema, so PAY-03 added `Card` support with no schema widening — only the
endpoint summary text changed; PAY-04's `/payments/split` is a genuinely
new route, so that regeneration produced real additive drift).

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None directly — the fiscal document is issued by `CloseOrderAsync`
independently of whether or when a payment is recorded. A `Payment` is not a
fiscal document and is never treated as one.

## Permissions

None — any authenticated terminal can record a payment against any order,
same as every other ordering endpoint today. Cash-handling accountability
in the stronger sense (a manager-only variance review, a sign-off) remains
future work — see [cash-sessions.md](cash-sessions.md#open-questions).

## Testing

**Backend:** covered indirectly through the endpoint's own validation logic
(method parsing, amount checks, balance computation, order lookup) — no
dedicated `Brasa.Api.IntegrationTests` class yet; the full backend suite
(101 tests) and `verify.ps1` (build, tests, OpenAPI drift, breaking-change
check, vulnerable-package scan) all pass with the module wired in.

**`src/web/e2e/tests/payments.spec.ts`** — 16 tests: a cash tender covering
the total with correct change; a tender smaller than the total recorded as a
valid partial payment, tracked correctly across a settling second tender
that itself overpays (proving `AmountApplied`/`Change`/`RemainingBalance`
compose correctly, not just in isolation), and a further tender against the
now-settled order rejected with `payment.already_settled`; zero/negative
amounts, an unsupported method (`'Voucher'`), and an unknown order each
rejected with the right code; listing payments across a partial-then-settling
pair (empty, then two rows, then 404 for an unknown order); recording a
payment *after* close to prove close isn't a precondition; a card tender
covering the total exactly with zero change; a card partial payment tracked
the same as cash, then an overpaying card tender rejected with
`payment.card_tender_exceeds_balance`, then the exact remaining balance
settling normally; a genuine cash+card split settling an order exactly, both
rows confirmed via a follow-up `GET /payments` (not just the split call's own
response body); an invalid tender partway through a split batch leaving zero
rows persisted, proving the all-or-nothing atomicity, plus an empty
`tenders` array rejected; a tip attributed to a real staff member with the
name resolved both on record and on list; an unattributed tip, a negative
tip, and an unknown attributed staff id each rejected with their own code;
and five real-browser UI tests — one full cash tender, one two-tender
partial-then-settle flow, one signing in then recording a tip
auto-attributed to the signed-in staff member, one switching the
payment-method `<select>` to Card and settling across a partial-then-exact
pair (the history list's own localized method label confirming the selected
method actually reached the recorded payment), and one filling the split
form's cash leg, watching the card leg auto-compute the remainder, and
settling in one click. `cashSessionId` (PAY-11) coverage lives in
`cash-sessions.spec.ts` instead, alongside the rest of that feature's
tests — see [cash-sessions.md](cash-sessions.md#testing).

## Open questions

- **Two terminals racing the same order's balance can over-settle it.**
  `RecordPaymentAsync` reads "prior payments" and writes the new one in two
  separate steps with no concurrency token guarding the order's aggregate
  balance — unlike `Order` itself (ORD-21's `xmin` token), a `Payment` row's
  own save can never conflict, because each terminal is inserting a *new*
  row, not updating a shared one. Two terminals that both read the balance
  as "still owed" before either write lands can each successfully record a
  payment, together exceeding the total. Low real-world likelihood (cash
  payments happen at a till, rarely two-at-once against the same order) but
  a real, unclosed gap — revisit if PAY-08's cash session work ever needs a
  stronger guarantee than "extremely unlikely in practice." The split
  endpoint (PAY-04) doesn't add a *new* exposure — its own tenders apply
  sequentially within one request against a balance read once at the start,
  the same single-snapshot shape the single-tender endpoint already has —
  but a split batch racing a *concurrent, separate* request (single or
  another split) against the same order is exposed to exactly this same gap.
- **No UI or API path to void or correct a recorded payment.** A mis-entered
  tender amount has no correction path today beyond direct database access —
  same class of gap as `Order`'s own void-a-line design solves for order
  lines, not yet solved here.
- **`PaymentMethod` has two cases (`Cash`, `Card`).** Adding `Card` (PAY-03)
  was additive to the enum and the endpoint's validation, not a redesign —
  and nothing about routing a card payment to an actual payment processor
  exists, or ever will for this manually-captured case: a standalone TPA is
  a separate physical device staff already ran before keying the amount in
  here. `MBWay`/an integrated TPA (PAY-13/14) are deferred past MVP.
- **A split (PAY-04) carries no tip or staff-attribution fields.** Crediting
  a tip to one portion of a split settlement is ambiguous — "which tender
  earned it?" — so PAY-04 deliberately left that unresolved rather than
  guessing; a guest who splits and tips needs the tip recorded on an
  ordinary single tender instead (before or after the split, since neither
  endpoint cares about order).
- **A card tender's "no overpay" rule trusts what staff key in.** Nothing
  verifies against the real TPA that the amount recorded actually matches
  what was charged — the same trust boundary `Cash` always had (nothing
  verifies a cash tender was really handed over either). This is a recording
  system, not a payment processor; reconciliation against a till/TPA total is
  PAY-08/10/11's *fecho de caixa* territory, not this.
