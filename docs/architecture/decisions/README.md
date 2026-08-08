# Architecture decision records

Records of choices that were **genuinely contested** — where a competent
developer would plausibly have chosen otherwise, and would waste time
re-litigating the question later.

Each one ends with a **"Revisit when"** section. That is the part that matters a
year from now: a decision without stated trigger conditions quietly becomes
dogma. Reopen a decision when one of its triggers is actually met, not because
it feels wrong.

## Index

| # | Decision | Status | In one line |
|---|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith, not microservices | Accepted | One deployable app with enforced internal boundaries; a solo developer cannot also operate a distributed system |
| [0002](0002-own-fiscal-engine.md) | Build our own AT-certified fiscal engine | Accepted | Partner per-document pricing is ruinous at restaurant volumes, and the certificate is the moat |
| [0003](0003-site-agent.md) | Introduce an in-restaurant Site Agent | Accepted | Offline signing, thermal printing and LAN sync are three problems with one answer |
| [0004](0004-react-pwa-not-blazor.md) | React PWA for clients, not Blazor | Accepted | A POS must cold-boot in ~2s on a cheap tablet and run offline, despite the C# backend |
| [0005](0005-plain-guid-ids.md) | Plain `Guid` ids; isolation enforced by RLS | Accepted | The database refusing to return rows beats compile-time typing that raw SQL bypasses |
| [0006](0006-no-mediatr.md) | Hand-rolled dispatcher instead of MediatR | Accepted | MediatR is now commercially licensed; we need ~150 lines of it |
| [0007](0007-client-agnostic-api.md) | One client-agnostic API for every platform | Accepted | Android and iOS must ship without backend change; a BFF per platform is the opposite of that |
| [0008](0008-token-auth-no-cookies.md) | Token auth, device-bound refresh, no cookies | Accepted | Cookie auth cannot serve native apps, and a POS terminal is trusted hardware, not a user |
| [0009](0009-incremental-delivery.md) | Incremental delivery, walking skeleton first | Accepted | Vertical slices with a demo script as the definition of done; certification stays binary regardless |
| [0010](0010-rls-runtime-role-split.md) | Split the DB role: unprivileged at runtime, superuser only for migrations | Accepted | The bootstrap Postgres role is a superuser, and superusers bypass RLS unconditionally — found via I0's first live test |

## Writing a new one

Copy the shape of an existing record:

```
# ADR NNNN — <decision in the imperative>

**Status:** Accepted · **Date:** YYYY-MM-DD

## Context      what forced a choice, and what the options were
## Decision     what we chose, stated plainly
## Consequences good AND bad — an ADR with no downsides is not honest
## Revisit when the trigger conditions that would reopen this
```

Number sequentially. Do not renumber or delete a superseded record — mark its
status as **Superseded by NNNN** and leave it, because the reasoning is still
what explains the code someone is reading.

Do not write an ADR for the obvious. Six exist for a foundation, which is about
the right density.
