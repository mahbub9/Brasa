# API contract

> **Status: design.** No API surface exists yet beyond `/health` and
> `/api/v1/ping`. These are the rules the API must obey from its first endpoint,
> so that shipping Android and iOS apps later needs **no backend change**.

## The requirement

Four client families are planned, on an undecided stack:

| Client | Auth | Offline | Realtime |
|---|---|---|---|
| Web POS (PWA) | Terminal pairing + staff PIN | **Full** | Yes |
| Staff handheld (Android/iOS) | Terminal pairing + staff PIN | **Full** | Yes |
| Owner dashboard (Android/iOS) | User account | No | Optional |
| Customer app (Android/iOS) | Consumer account | No | Optional |
| Kitchen display (PWA or native) | Terminal pairing | Degraded | Yes |

"No backend change" is achievable, but it is not free. It means **building
specific seams now** — auth, version negotiation, push registration, sync — and
then not breaking them. Everything else on this page is discipline, which costs
nothing as long as it is never violated.

---

## 1. Two API surfaces, never one

```
/api/v1/**          Tenant API   — staff, managers, terminals. Tenant-scoped, RLS enforced.
/api/public/v1/**   Consumer API — customers. No tenant staff data. Separate auth realm.
```

A customer must **never** authenticate against the same identity system as
restaurant staff. Collapsing the two is the mistake that later forces either a
rewrite or a security incident. QR self-ordering already uses the consumer
surface, so this split exists before the customer app does.

## 2. Authentication: tokens, never cookies

**Cookies are web-specific.** A cookie-authenticated API cannot serve a native
app without backend change — which is precisely what we are avoiding.

- **OAuth 2.1 / OIDC with PKCE** for interactive sign-in. Native apps cannot keep
  a client secret, so PKCE is not optional.
- **Short-lived access token** (JWT, minutes) + **long-lived refresh token**
  (opaque, rotating, device-bound).
- **Every refresh token is bound to a device record.** A waiter's lost phone must
  be revocable individually, without signing out the restaurant.
- **Bearer tokens in `Authorization`.** No session state on the server.

### Terminals versus users

A device becomes a POS terminal by **pairing** — a short-lived device code
entered in the back-office — and receives terminal credentials. Staff then sign
in *on that terminal* with a PIN.

This is one flow, identical for a browser, an Android tablet and an iPhone. The
PIN is a fast in-shift identity switch on an already-trusted device, **never a
primary credential over the internet**.

See [decisions/0008-token-auth-no-cookies.md](decisions/0008-token-auth-no-cookies.md).

## 3. Client version negotiation

This is the rule that exists *only* because of mobile, and the one most often
missed until launch.

**A web client updates on refresh. A mobile app does not.** Old versions persist
for months — users disable auto-update, devices sit on old OS versions, review
delays hold back releases. The backend must therefore assume it is always
serving clients it cannot upgrade.

Every request carries:

```
X-Brasa-Client: <client-id>/<version> (<platform>)
e.g.  X-Brasa-Client: pos-handheld/2.4.1 (android)
```

And the API exposes:

```
GET /api/v1/client-requirements
→ { "minimumSupported": "2.0.0", "recommended": "2.4.1", "sunsetAfter": "2027-03-01" }
```

so an app can present *"update required"* on its own, without the backend
guessing.

**Version policy:** `/api/v1` supports the current and previous major version at
minimum. Deprecations are announced with RFC 8594 `Deprecation` and `Sunset`
response headers before removal. A breaking change means `/api/v2`, never a
silent change to `v1`.

## 4. Every realtime message has a REST equivalent

Realtime is SignalR, using the **JSON protocol** (not MessagePack) so any
platform can implement it. Official clients exist for TypeScript and Java;
Swift and Dart have community clients.

**The hard rule:** realtime is an *optimisation*, never the only route to data.
Anything delivered over a socket must also be reachable by polling the sync
endpoints. A client on a platform with no usable SignalR library must still be
able to function — degraded, but correct.

This single rule removes the entire risk of realtime-library availability
dictating platform choice.

## 5. Sync is plain REST, cursor-based

```
POST /api/v1/sync/push     idempotent batch of client mutations (the outbox)
GET  /api/v1/sync/pull?cursor=<opaque>   server changes since the cursor
```

- **Cursor-based, never timestamp-based.** Device clocks are wrong — a handheld
  left in a drawer over a weekend will disagree with the server. A timestamp
  cursor silently loses records; an opaque server-issued cursor cannot.
- Identical for a browser PWA, React Native, Flutter or native. The offline
  engine is a client concern; the protocol is not.

## 6. Idempotency on every mutation

```
Idempotency-Key: <client-generated uuid v7>
```

Required, not optional. Mobile networks fail mid-request constantly, and a
retried "close table" must not produce two fiscal documents. This is already a
project-wide rule; mobile makes it load-bearing.

Ids are UUIDv7 generated **on the client**, so an offline device mints its own
without a round trip.

## 7. Push notifications

Register the seam now; add provider adapters later.

```
POST   /api/v1/devices/{deviceId}/push-tokens   { provider: apns|fcm|webpush, token }
DELETE /api/v1/devices/{deviceId}/push-tokens/{id}
```

Backend code depends on `IPushChannel`, never on a provider SDK. Adding APNs and
FCM later is **additive** — a new adapter behind an existing interface — not a
change to any calling code.

> Honest caveat: the adapters themselves are backend code written later. What
> this design guarantees is that nothing *already written* has to change.

## 8. Errors are stable machine-readable codes

`Error.Code` values such as `order.already_closed` are a **public contract**.

**Once released, an error code never changes meaning.** A mobile client branches
on it and cannot be patched quickly.

The server returns **codes, not display strings**. Clients localise. Mobile apps
bundle their own translations and must not depend on server-side wording, which
also means an app in Portuguese works against an English-defaulting server.

## 9. Payload discipline

Assume a phone on cellular in a basement dining room.

- **Cursor pagination on every collection.** No unbounded lists, ever.
- **`ETag` / `If-None-Match`** on configuration and menu pulls — most syncs
  should return `304`.
- **Compression** on all responses.
- **No chatty designs.** A screen should need one request, not fifteen.
- **Media via presigned URLs**, uploaded direct to storage, never proxied
  through the API.

## 10. Feature flags carry a platform dimension

Flags are keyed by tenant **and** client platform, so a feature can ship to web
first and reach mobile a release later without a backend change.

## 11. No backend-for-frontend

One API serves every client. A BFF per platform is exactly the "new backend code
per client" this document exists to prevent.

Where clients genuinely need different shapes, use sparse fieldsets or a
purpose-built endpoint that any client may call — not a platform-specific tier.

## 12. Deep links

Served as static well-known documents so app links work without app-specific
backend logic:

```
/.well-known/apple-app-site-association
/.well-known/assetlinks.json
```

## 13. OpenAPI is the contract, and CI enforces it

- The OpenAPI document is generated from the API and **committed**, so a
  contract change is visible in review.
- CI runs **breaking-change detection** against the previous document. A change
  that would break an already-shipped client fails the build.
- Clients are **generated**, never hand-written: TypeScript today; Kotlin, Swift
  or Dart later from the same document, whichever stack is chosen.

This is what actually makes the guarantee real. Discipline decays; a failing
build does not.

---

## What must be built now

Ordered by cost of retrofitting, most expensive first.

| # | Seam | Why it cannot wait |
|---|---|---|
| 1 | Token auth with PKCE + device-bound refresh | Cookie auth would have to be torn out and every client re-authenticated |
| 2 | Device registry and terminal pairing | Push tokens, revocation and terminal identity all hang off it |
| 3 | Cursor-based sync endpoints | Changing the sync protocol later breaks every offline client at once |
| 4 | Idempotency keys | Retrofitting means auditing every mutation for double-effect |
| 5 | Client version header + requirements endpoint | Without it, the first mobile release can never be safely deprecated |
| 6 | Stable error codes | Codes become load-bearing the moment an app ships |
| 7 | Push token registration + `IPushChannel` | Cheap now; a schema migration and API addition later |
| 8 | OpenAPI generation + breaking-change CI | The only thing that keeps the rest honest |

## What is deliberately deferred

- APNs and FCM provider adapters — additive behind `IPushChannel`
- The consumer identity realm's social/OTP providers — the surface split matters
  now, the providers do not
- Per-platform rate limits — the `X-Brasa-Client` header makes them addable
  without a contract change
