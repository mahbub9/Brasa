# Realtime floor updates

> **Status:** ✅ built — this codebase's first realtime channel
> **Module:** Web (`Brasa.Api`, `pos`)
> **Roadmap:** I3 (pulled forward)

## What it is

When one terminal opens a table, clears a dirty one, requests the
bill, transfers or merges an order, every other terminal's floor
picker updates live — no manual refresh, no polling. A second waiter
looking at the table picker sees a table flip from `Free` to
`Occupied` the moment the first waiter seats a party there.

## Why it works this way

**A payload-less signal, not a data push.** `FloorHub` broadcasts one
event, `FloorChanged`, carrying no data at all. A client that receives
it always re-fetches `GET /floor` — the REST equivalent hard rule 7
requires every realtime message to have (API-17). This is satisfied by
construction rather than a bolted-on check: there is nothing in an
empty payload that could ever disagree with the REST response, because
there is no payload to disagree with. It also means Floor's own DTO
shape can evolve freely without a second place to keep in sync.

**Broadcast to everyone, not targeted per tenant or terminal.** No
terminal identity exists yet (IDN-06/07, I3's own device-pairing
work), so there is nothing to scope a SignalR group by. Every
connection sits in one `Clients.All` group — harmless at today's
single-dev-tenant scale, a named and deferred gap once real
multi-tenancy needs each tenant's own connections isolated from every
other tenant's floor changes.

**Six call sites, one shared helper.** Table state changes happen in
two different files — `FloorEndpoints.cs` (`ClearTableAsync`,
`RequestBillAsync`) and `OrderEndpoints.cs` (`OpenOrderAsync`,
`TransferOrderAsync`, `MergeOrdersAsync`, `CloseOrderAsync`) — composing
`IHubContext<FloorHub>` the same way these handlers already compose
Floor and Ordering at the API layer (see
[../architecture/module-boundaries.md](../architecture/module-boundaries.md)
rule 5). Every one of the six calls the same `FloorHubBroadcast.NotifyFloorChangedAsync`
helper right after its own successful save — one shared, typo-proof
method name rather than six separately-typed string literals.

**No credentials on the connection.** This codebase has no cookie auth
to carry (hard rule 7) — `pos`'s SignalR client connects with
`withCredentials: false` explicitly, matching the plain, unauthenticated
REST calls it already makes.

## Behaviour

1. `pos` connects to `FloorHub` once, on app mount, and re-fetches
   `GET /floor` on every `FloorChanged` signal.
2. Any of the six table-state-changing actions above — on **any**
   terminal — broadcasts `FloorChanged` to every connected client
   immediately after its own database save succeeds.
3. A connection drop is handled by automatic reconnect
   (`@microsoft/signalr`'s own `withAutomaticReconnect()`); on
   reconnect, `pos` re-fetches `GET /floor` once more, in case a signal
   was missed entirely during the gap.
4. A client that never connects at all, or whose connection is
   currently down, simply doesn't see live updates — it still shows
   correct data on its next explicit action (opening a table, clearing
   one), since every mutation's own response already reflects the
   change regardless of whether the push arrived.

## Offline behaviour

Not applicable in the SYN-epic sense (no offline queue, no outbox) —
but degrades gracefully by design: a failed or dropped SignalR
connection never blocks or breaks anything else. `GET /floor` remains
the authoritative source of truth on every explicit action; the hub is
purely an optimisation that avoids a manual refresh, never the only
route to current state (the same hard-rule-7 framing this whole
feature is built around).

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| SignalR connection fails to establish | Caught and swallowed in `connectFloorHub` | Floor picker still works via ordinary `GET /floor` calls; simply no live push until a connection succeeds |
| Connection drops mid-session | Automatic reconnect; re-fetches floor state once reconnected | A brief window of staleness, self-healing once reconnected |
| `TestClockMiddleware`/dev-only middleware reaches Production | N/A to this feature directly, but the same "throw on the first request if `IsProduction()`" pattern protects `FloorHub` from nothing — it has no such guard, since broadcasting floor state has no fiscal or security sensitivity | — |

## Data

No new persisted state. `FloorHub` and `FloorHubBroadcast` are pure
transport — nothing about `Table`/`Room` changes because of this
feature.

## API

| Method | Route | Purpose |
|---|---|---|
| n/a (SignalR) | `/hubs/floor` | Realtime hub — outside `/api/v1`, since a connection isn't a versioned REST resource |

No REST endpoint of its own; every table-state REST endpoint that
already existed now also triggers a broadcast as a side effect.

## Integration events

None — this is a within-process SignalR broadcast, not a cross-module
integration event (`docs/architecture/module-boundaries.md`'s outbox
pattern is a separate, still-unbuilt concern, FND-11/12).

## Fiscal impact

None. Purely a floor-plan/operations concern — no fiscal document is
issued, referenced, or affected by any broadcast.

## Permissions

None enforced — the hub has no concept of who's connected yet, since
no terminal or staff identity exists (IDN-06/07). Anyone who can reach
the API can connect to `/hubs/floor` and receive every broadcast, the
same trust level every other unauthenticated endpoint in this codebase
has today.

## Testing

`floor-realtime.spec.ts` — two real, separate Playwright browser tabs,
neither ever calling `reload()`: opening a table from the second tab
flips it to `Occupied` in the first purely from the pushed signal, and
clearing a dirty table flips it back to `Free` the same way. Proves
the live push itself, not a disguised poll.

## Open questions

- No per-tenant/per-terminal targeting — every connection is
  broadcast-to-all. Real multi-tenancy needs this scoped before it can
  ship past a single dev tenant.
- Only Floor state changes broadcast anything. Order-level realtime
  (e.g. a kitchen ticket firing) is unbuilt — this is a first, narrow
  slice of API-16/17, not the whole realtime surface the roadmap
  eventually implies (KDS, I4).
- `admin` does not subscribe to `FloorHub` at all yet — its own floor
  editor is a management screen, not a live-service view, so the value
  of a live push there is more marginal; left unbuilt rather than
  built speculatively.
