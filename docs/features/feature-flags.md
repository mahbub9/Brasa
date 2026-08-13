# Feature flags

> **Status:** ✅ built
> **Module:** Identity
> **Roadmap:** I3 (pulled forward)

## What it is

A per-tenant, optionally per-platform on/off switch: `admin` can create a
flag by key, toggle it, and optionally override it for one platform (e.g.
`web`, `ios`) while every other platform keeps the tenant's default. No
feature in this codebase reads one yet — this ships the mechanism a real
consumer picks up later, the same "mechanism before the trigger" shape
CAT-05/CAT-10/CAT-16/FLR-05 already established.

## Why it works this way

**No consumer yet, and that's deliberate.** IDN-16 was named as one of the
"scale decisions to make on day one" in this project's own build plan —
cheap now, expensive to retrofit once something mid-rollout actually needs
one. Nothing in this codebase currently needs to gate on a flag: there is
no native app to stage a rollout to (the MOB epic), and no paid-tier story
to key a feature off of yet (IDN-02…05, IDN-13). Building the mechanism
now, unused, is the same bet CAT-05 (price lists) and CAT-10 (combos) each
made before their own first real trigger existed.

**`Platform` is never `null`.** The obvious modelling choice — a nullable
`Platform` column meaning "no override, applies everywhere" — has a real
bug hiding in it: Postgres unique indexes treat every `NULL` as distinct
from every other `NULL`, so a `(TenantId, Key, Platform)` unique
constraint would have silently allowed two "all platforms" rows for the
same key to coexist, the exact case a flag is most likely to actually be
used in (most flags will never need a platform override at all).
`FeatureFlag.AllPlatforms` (`"all"`) is an explicit sentinel string
instead, so the same "domain guard plus a DB unique index" defence this
codebase already uses elsewhere (e.g. `PriceListEntry`) actually holds for
every case, not all-but-one.

**Free-form platform string, not a closed enum.** `ClientInfo.Platform`
(API-06) already parses a free-form lowercase platform name out of the
`X-Brasa-Client` header for a different purpose (client version
negotiation) — a flag's own `Platform` uses the same shape and the same
vocabulary in practice, deliberately not unified into one shared type: one
is parsed from a client-supplied header and never persisted, the other is
tenant configuration that always is. Coupling them would make an API
client's header vocabulary a schema migration away from a back-office
admin's flag vocabulary.

**Resolve, not "get," is the shape a real consumer will actually call.**
`GET /feature-flags/{key}/resolve?platform=X` — not a raw row lookup —
because a real caller never wants "does a row exist," it wants "is this
on for me," including the platform-specific-overrides-the-default
fallback and the unconfigured-defaults-to-off rule. Doing that resolution
once, on the server, means every future consumer (web, iOS, Android
alike) gets identical fallback behaviour for free instead of each
re-implementing it.

**No delete.** The same "create + list only" narrow first slice IDN-01
itself shipped as — turning a flag off already covers "stop gating on
this," and with no real consumer yet, nothing depends on a row's absence
specifically (as opposed to its `isEnabled: false` value).

## Behaviour

1. Admin sets a flag: `PUT /feature-flags/{key}` with `{ platform?, isEnabled }`.
   `platform` omitted or blank defaults to `"all"`. Creates the row if it
   doesn't exist yet for that exact key/platform pair, otherwise updates
   `isEnabled` in place.
2. Admin lists every flag for the tenant: `GET /feature-flags`.
3. A consumer resolves whether a flag is on: `GET /feature-flags/{key}/resolve?platform=X`.
   A platform-specific row wins over the tenant's `"all platforms"` row;
   if neither exists, the flag resolves to `isEnabled: false` — a flag
   nobody has ever configured must never silently enable whatever it
   gates.
4. `admin`'s "Feature flags" screen lists every key (grouped, since a key
   with a platform override reads as one flag with an exception, not two
   unrelated rows), each row showing its platform and an Enabled/Disabled
   toggle, plus a form to add a new key.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today, and no client
(`pos`/`kds`/agent) resolves a flag yet.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| `PUT`/`GET .../resolve`'s `key` route segment is missing, empty or whitespace | Rejected | `400 identity.invalid_feature_flag_key` |
| A key nobody has ever configured is resolved | Not an error — resolves to `isEnabled: false` | `200` with `isEnabled: false` |
| A key/platform pair is set twice | Not an error — the second call updates `isEnabled` in place, not a second row | `200` with the new value |

## Data

`FeatureFlag` (`Key`, `Platform` — never null, `"all"` is the sentinel for
every platform, `IsEnabled`) — owned by Identity. Unique on
`(TenantId, Key, Platform)`, enforced by both a domain-level
find-or-create check in the endpoint and a DB unique index — the same
"domain guard plus a DB backstop" shape `PriceListEntry` already uses.

## API

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/feature-flags/{key}` | Create or update a flag, optionally scoped to one platform |
| `GET` | `/feature-flags` | List every flag configured for the tenant |
| `GET` | `/feature-flags/{key}/resolve` | Resolve whether a flag is on for a platform (query param `platform`, defaults to `"all"`) |

`PUT` takes `Idempotency-Key` like every other mutation. `GET` routes are
plain reads.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. Purely a platform/rollout mechanism.

## Permissions

None enforced — same "ships ahead of manager authorisation" shape every
other admin mutation in this codebase has today.

## Testing

`feature-flags.spec.ts` — setting a flag with no platform defaults it to
`"all"` and it appears on a fresh `GET /feature-flags`; a platform-specific
override wins over the tenant's `"all platforms"` row on resolve for that
platform, while an unrelated platform still falls back to the default; a
key nobody has ever configured resolves to `isEnabled: false`, not `true`;
re-setting the same key/platform updates the existing row rather than
creating a second one; an empty key is rejected; the `admin` UI adds a
flag and toggles it off through a real browser, confirmed via the visible
badge text, not just a follow-up API call.

## Open questions

- No delete endpoint — see "Why it works this way" above.
- No consumer anywhere in this codebase yet reads a flag to actually gate
  behaviour — the mechanism is proven, the trigger is still open.
- No admin-facing search/filter once the flag list grows past a handful —
  not needed yet at the scale a single tenant configures today.
