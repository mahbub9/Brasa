# ADR 0007 — One client-agnostic API for every platform

**Status:** Accepted · **Date:** 2026-08-08

## Context

Android and iOS apps are planned shortly after the web launch, covering four
client families: staff handheld ordering, an owner dashboard, a customer
ordering app, and a kitchen display. The mobile stack is undecided — React
Native, Flutter and native Kotlin/Swift are all still open.

The stated requirement is that **shipping those apps must require no backend
change**.

The usual way this fails is not dramatic. The API quietly acquires web-specific
assumptions — a session cookie, a CSRF token, an endpoint returning HTML-ready
strings, an unbounded list that is fine on Wi-Fi — and each one becomes a
backend change at mobile launch.

Two alternatives were considered:

| Option | Why rejected |
|---|---|
| **Backend-for-frontend per platform** | A BFF per client *is* new backend code per client. It is the exact outcome we are avoiding, dressed as architecture |
| **GraphQL** | Solves client-shaped payloads well, but adds a query-complexity and caching burden, and offline sync still needs purpose-built endpoints. The payload problem is better solved by cursor pagination and sparse fieldsets |

## Decision

**One versioned REST API serves every client**, governed by the rules in
[../api-contract.md](../api-contract.md). The load-bearing ones:

1. **Two surfaces**: `/api/v1` (tenant — staff, terminals) and
   `/api/public/v1` (consumer — customers). Never one.
2. **Tokens, never cookies** — see [0008](0008-token-auth-no-cookies.md).
3. **Client version negotiation** via an `X-Brasa-Client` header and a
   `client-requirements` endpoint, because mobile apps cannot be force-updated.
4. **Every realtime message has a REST equivalent.** Realtime is an
   optimisation, never the only route to data.
5. **Sync is cursor-based REST**, never timestamp-based — device clocks are
   wrong.
6. **Errors are stable machine-readable codes**; clients localise.
7. **OpenAPI is committed, and CI fails on breaking changes.**

## Consequences

**Good**

- Adding a platform means generating a client from the existing OpenAPI
  document. No backend work.
- The stack decision stays open. Nothing here favours React Native over Flutter
  or native.
- The discipline improves the web client too: cursor pagination and conditional
  requests matter on a restaurant's overloaded Wi-Fi as much as on cellular.
- The consumer/tenant split exists before the customer app does, so QR
  self-ordering is built on the right surface from the start.

**Bad**

- More work up front. Token auth with PKCE and a device registry is
  meaningfully more than issuing a session cookie.
- Sustained discipline required. One endpoint returning a localised string, or
  one unbounded collection, erodes the guarantee.
- Some payloads will be larger than a client-tailored response would be. Sparse
  fieldsets mitigate this; a BFF is not the answer.

**The guarantee, stated honestly**

Nothing *already written* has to change. New capability still needs new code —
APNs and FCM adapters, for instance, are written later. They are additive behind
`IPushChannel`, and no existing caller is touched.

## Enforcement

Discipline decays; a failing build does not. CI generates the OpenAPI document
and runs breaking-change detection against the committed previous version. A
change that would break an already-shipped client fails the build.

## Revisit when

- A client genuinely cannot be served without a platform-specific endpoint, and
  sparse fieldsets have been tried and are insufficient.
- Payload size becomes a measured problem on cellular rather than a suspected
  one.
- The realtime fallback proves unusable on a platform we have actually shipped
  to.
