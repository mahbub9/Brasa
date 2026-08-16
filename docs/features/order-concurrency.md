# Order concurrency — a lost-update guard for the order aggregate

> **Status:** ✅ done — the mechanism and its 409 contract, not the UI reaction to it.
> **Module:** Ordering (the token and every mutating endpoint), Fiscal (one deliberately different call site)
> **Roadmap:** I3 (`ORD-21`, pulled forward)

## What it is

Two terminals working the same table can genuinely race: a waiter adds a
line at the exact moment a manager voids one, or closes it. Before this,
`Order` carried no concurrency token at all — every mutating endpoint did a
blind `UPDATE ... WHERE Id = @id`, so the second writer's save simply
overwrote the first's with no error, no warning, and no trace either change
ever happened. `Order` now carries the same `xmin`-based optimistic
concurrency token [`Table` already used](floor-plan-editor.md) for the exact
same reason (the table-occupy race found in I0). Every order-mutating
endpoint turns a lost race into a clean `409 order.concurrently_modified`
instead of a silent lost update.

## Why it works this way

**`xmin`, not a hand-rolled version column.** Postgres's own row-version
system column already exists on every row with no migration needed — the
identical mechanism and the identical trap
(`ALTER TABLE ... ADD COLUMN xmin` fails outright, since Postgres reserves
that name) `TableConfiguration.cs` already worked through. The migration
this shipped with is deliberately empty, the same fix
`AddTableXminConcurrencyToken` used.

**One shared helper, not nine copy-pasted `try`/`catch` blocks.**
`OrderEndpoints.TrySaveOrderAsync` wraps `SaveChangesAsync`, catches
`DbUpdateConcurrencyException`, and returns `order.concurrently_modified` —
every endpoint that mutates an already-loaded `Order` (`AddLine`,
`AddComboLine`, line notes/quantity/discount, void, fire, order discount,
transfer-line, transfer-order, merge) calls it instead of saving directly.

**`CloseOrderAsync` deliberately does not use that helper.** By the time its
own save could lose the race, `IFiscalProvider.IssueSimplifiedInvoiceAsync`
has already issued a real fiscal document — and `CLAUDE.md`'s hard rule 3
means that document can never be un-issued.
A generic "reload and try again" response would invite exactly the wrong
client behaviour here: retrying would call the fiscal provider a *second*
time for what the caller believes is still one close attempt, risking a
duplicate document for a single real close. So this one call site catches
the same exception but returns a distinct, non-generic code instead —
`order.close_conflict_after_fiscal_issuance` — whose message explicitly says
not to retry. This is the same "I0's correctness floor, not I5's durable
two-phase guarantee" boundary `CloseOrderAsync`'s own pre-existing comment
already named for the fiscal-failure case; ORD-21 doesn't close that gap, it
makes the *other* half of it (a lost race, not a fiscal failure) loud
instead of silent, which is a real improvement even though the full
durable guarantee is still deferred to the outbox-based work in I5+.

**Safe to retry everywhere else, on purpose.** Every other call site's own
conflict happens before anything external occurs — no fiscal document, no
table state change — so the shared helper's message is a plain "reload and
try again," and a client can safely do exactly that.

## Behaviour

1. Two terminals load the same order (directly, or by acting on it close
   enough in time that both reads land before either write).
2. Both attempt a mutation and both call `SaveChangesAsync`.
3. The first save to reach Postgres wins normally — its `UPDATE ... WHERE
   Id = @id AND xmin = @original` matches the row and commits, advancing
   `xmin`.
4. The second save's own `WHERE` clause no longer matches (the row's
   `xmin` has moved on) — Postgres reports zero rows affected, and EF
   Core throws `DbUpdateConcurrencyException`.
5. Every mutating endpoint except `CloseOrderAsync` catches this and
   returns `409 order.concurrently_modified` — safe to retry, since
   nothing outside the database changed. `CloseOrderAsync` catches the
   same exception but returns `409 order.close_conflict_after_fiscal_issuance`
   instead, since its own fiscal document was already issued by this point.

## Offline behaviour

Not applicable — cloud API endpoints, no offline path today. The eventual
offline sync engine (SYN, I5) will need its own conflict-resolution policy
per entity type (`SYN-05`) — this token is a building block for that, not a
substitute.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Two terminals both mutate the same order (add line, void, discount, notes, quantity, fire, order discount, transfer-line) before either commits | The loser's save is rejected, its change never silently applied | `409 order.concurrently_modified` — safe to reload the order and retry |
| Two terminals race a table transfer or an order merge | Whichever order's own row lost the race is reported the same way — the message stays generic since either of two orders could be the one that raced | `409 order.concurrently_modified` |
| A concurrent mutation races a close *after* the fiscal document for that close was already issued | The order's own row fails to persist as `Closed`, but the document is real and already on file — this is surfaced, not silently retried | `409 order.close_conflict_after_fiscal_issuance` — the message explicitly says not to retry; a human reconciles the order against the fiscal document already issued |

## Data

No new persisted column — `xmin` is a Postgres system column, already
present on every row. `OrderConfiguration.cs` maps it as a shadow property
via `IsRowVersion()`, the same shape `TableConfiguration.cs` uses. Dropped
for any non-Postgres provider (`OrderingDbContext.OnModelCreating`, mirroring
`FloorDbContext`) — SQLite (the beta pilot's provider, [ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md))
has no equivalent column, an accepted trade-down for that one-tenant pilot
only, same as `Table`'s own xmin already was.

## API

No new routes and no request/response shape changes — this is purely a new
possible error response on already-shipped endpoints (`POST /orders/{id}/lines`,
`POST /orders/{id}/combo-lines`, `PUT /orders/{id}/lines/{lineId}/notes`,
`PUT /orders/{id}/lines/{lineId}/quantity`, `PUT /orders/{id}/lines/{lineId}/discount`,
`POST /orders/{id}/lines/{lineId}/void`, `POST /orders/{id}/fire`,
`PUT /orders/{id}/discount`, `POST /orders/{id}/transfer`,
`POST /orders/{id}/lines/{lineId}/transfer`, `POST /orders/{id}/merge`,
`POST /orders/{id}/close`). `docs/openapi/v1.json` is unaffected — error
responses aren't documented there at all yet (see API-15's own noted gap),
only success shapes.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

Indirect but real: this is what stops a lost concurrent update from
silently reaching the fiscal document builder with a stale view of the
order. `CloseOrderAsync`'s own special-cased response exists specifically
to protect hard rule 3 (never mutate an issued fiscal document) from a
naive client retry after this exact conflict.

## Permissions

None — this applies regardless of which staff member or role is acting.

## Testing

**Deterministic, no timing dependence at all:**
`Brasa.Api.IntegrationTests/OrderConcurrencyIntegrationTests.cs` — two
independent `OrderingDbContext` instances, standing in for two terminals,
both load the same order before either writes; the first save wins, the
second's `SaveChangesAsync` throws `DbUpdateConcurrencyException`
deterministically every run, and the database is confirmed to hold only the
winner's change afterward. Finding this test's own migration step racing
against `TenantIsolationIntegrationTests`'s identical pattern (both mutate
the process-wide `BRASA_MIGRATIONS_CONNECTION` environment variable around
an async migration call, and xUnit runs different test classes in parallel
by default) is itself documented in `MigrationsEnvVarCollection.cs` — a
real bug this task's own test coverage found, not a hypothetical one.

**Real HTTP, honestly probabilistic:** `src/web/e2e/tests/order-concurrency.spec.ts`
fires several genuinely concurrent `POST /orders/{id}/lines` requests at the
same order and asserts the order ends up with exactly one line per
successful response — no lost update and no duplicate, whether or not the
race actually manifests on a given run. It does not assert a conflict must
occur: tried forcing one first (8, then 40 concurrent requests) and it never
landed in this environment — local Kestrel/Postgres round trips are fast
enough that even 40 concurrent requests didn't reliably overlap at the
`SaveChangesAsync` level. The deterministic integration test above is what
actually proves the 409 mechanism fires; this spec proves the client-visible
contract (still-consistent state, and a well-formed `order.concurrently_modified`
body whenever a 409 does happen) end-to-end.

**Not separately tested:** the `order.close_conflict_after_fiscal_issuance`
code path. It mirrors the already-proven `TrySaveOrderAsync` pattern closely
enough that its correctness is verified by construction and code review
rather than a forced live race — reliably provoking that specific
close-loses-to-a-concurrent-mutation interleaving would need a test-only
seam this codebase doesn't have (something like `X-Brasa-Test-Clock` but for
pausing mid-request), which felt like real scope creep for one narrow error
branch. Named here rather than left silent.

## Open questions

- No client UI reacts to either new code beyond `pos`'s generic error
  banner (localized via `error.code.order.concurrently_modified` /
  `error.code.order.close_conflict_after_fiscal_issuance` in
  `resources/{pt,en}.ts`, the same `describeError()` mechanism WEB-13
  already established) — no retry button, no automatic reload. `admin`'s
  own error-code dictionary still doesn't exist at all (a pre-existing gap
  WEB-13's own row already names).
- `order.close_conflict_after_fiscal_issuance` tells staff to "check the
  order and the fiscal document, then reconcile by hand" — there's no
  tooling anywhere in this codebase yet to actually do that reconciliation;
  it's a correct, honest error message pointing at a manual process, not a
  built one.
- `TransferLineAsync`/`MergeOrdersAsync` can lose the race on *either* of
  two orders; the response deliberately doesn't say which, since naming the
  wrong one would be worse than naming neither.
- A cross-`DbContext` version of the same gap already existed and stays
  open: `TransferOrderAsync`'s Floor-side table swap commits before its
  Ordering-side save, so a newly-possible (if narrow) race could leave the
  table moved but the order's own `TableId` unchanged. Not solved here —
  true atomicity across two `DbContext`s is exactly the two-phase guarantee
  this feature's own remarks above defer to the I5+ outbox work.
