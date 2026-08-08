# ADR 0008 — Token authentication with device-bound refresh; no cookies

**Status:** Accepted · **Date:** 2026-08-08

## Context

The web POS alone would be well served by an HTTP-only session cookie: simple,
secure by default, CSRF handled by a framework, no token storage to get wrong.

But native Android and iOS apps are planned, and a cookie-authenticated API
cannot serve them without backend change. Cookie auth would therefore have to be
torn out and every client re-authenticated — the single most expensive retrofit
on the list in [../api-contract.md](../api-contract.md).

A second constraint is specific to this product: a **POS terminal is not a
user**. A tablet on a bar counter is trusted hardware that many staff use in
sequence, switching identity dozens of times an hour. Modelling each waiter as
performing a full sign-in per order is unusable in service.

## Decision

### Transport

**Bearer tokens in the `Authorization` header. No authentication cookies.**

- **OAuth 2.1 / OIDC with PKCE** for interactive sign-in. A native app cannot
  keep a client secret, so PKCE is mandatory, not a nicety.
- **Access token**: JWT, short-lived (minutes), stateless.
- **Refresh token**: opaque, long-lived, **rotating**, and **bound to a device
  record**.

### Two-stage identity: pair the device, then identify the person

1. **Terminal pairing.** A device is paired via a short-lived code issued in the
   back-office, and receives terminal credentials. It is now trusted hardware
   belonging to one site.
2. **Staff PIN.** On a paired terminal, staff identify themselves with a PIN to
   open a shift or authorise an action.

The PIN is a **fast identity switch on already-trusted hardware — never a
primary credential over the internet**. A four-digit PIN is not an
authentication factor on its own; it is only meaningful because the terminal was
already authenticated.

This flow is identical for a browser, an Android tablet and an iPhone, which is
the point.

### Consumer identity is a separate realm

Customers authenticate against `/api/public/v1` with their own identity system.
A customer account can never resolve to a tenant staff principal.

## Consequences

**Good**

- Native apps work against the same API with no backend change.
- **Per-device revocation.** A waiter's lost phone is revoked individually,
  without signing the restaurant out — which with a shared cookie session would
  be the only option.
- No server-side session state, so horizontal scaling stays trivial.
- Refresh-token rotation makes theft detectable: a replayed token indicates
  compromise and can invalidate the family.
- PIN entry stays instant during service.

**Bad**

- Token storage becomes a client responsibility, and getting it wrong is a real
  risk. Native apps must use the Keychain / Keystore; web clients must not put
  refresh tokens in `localStorage`.
- More moving parts than a cookie: rotation, device records, revocation lists.
- We must implement or host OAuth flows rather than getting sessions free from
  the framework.

**Mitigations**

- Web clients hold the refresh token in a `Secure`, `HttpOnly`, `SameSite=Strict`
  cookie *scoped to the token endpoint only*. This is a storage mechanism for
  one endpoint, not cookie authentication — every API call still carries a
  bearer token, so the API stays identical for native clients.
- Native clients use platform secure storage.
- Short access-token lifetimes bound the damage from a leaked access token.

## Revisit when

- A hosted identity provider (Entra ID, Auth0, Keycloak) becomes cheaper to
  operate than what we maintain — the token *shape* here is standard OIDC, so
  such a move is a swap, not a redesign.
- Portuguese or EU regulation imposes specific authentication requirements on
  POS operator identity.
