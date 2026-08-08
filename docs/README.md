# Documentation

Start here. Everything a new developer needs, in the order they need it.

> **Picking this up as an AI session?** Read
> **[ai/README.md](ai/README.md)** instead — it is a single dense brief written
> so you can be productive without scanning the repository.

## First hour

| Read | Why |
|---|---|
| [development/getting-started.md](development/getting-started.md) | Get it building and running locally |
| [architecture/README.md](architecture/README.md) | The three-tier shape and *why* it is that shape |
| [fiscal/README.md](fiscal/README.md) | The legal constraint that drives every other decision |
| [product/status.md](product/status.md) | What actually exists today versus what is only scaffolded |
| [glossary.md](glossary.md) | Portuguese fiscal and restaurant terms used everywhere |

## Session pickup

| Doc | Purpose |
|---|---|
| [ai/README.md](ai/README.md) | The brief: invariants, current state, next task, traps |
| [ai/repo-map.md](ai/repo-map.md) | Every tracked file, its purpose and its state |

## Features

One page per feature — behaviour, edge cases, offline handling and intent.

- [features/README.md](features/README.md) — index
- [`_template.md`](https://github.com/mahbub9/Brasa/blob/main/docs/features/_template.md) — template for a new page

## Architecture

- [architecture/README.md](architecture/README.md) — system overview
- [architecture/api-contract.md](architecture/api-contract.md) — the rules that let mobile apps ship without backend change
- [architecture/site-agent.md](architecture/site-agent.md) — the in-restaurant process
- [architecture/multi-tenancy.md](architecture/multi-tenancy.md) — tenant isolation and RLS
- [architecture/module-boundaries.md](architecture/module-boundaries.md) — the rules modules obey
- [architecture/money.md](architecture/money.md) — why money is integer cents, and bill splitting
- [architecture/conventions.md](architecture/conventions.md) — code conventions and analyzer policy

## Decisions (ADRs)

Records of choices that were not obvious, and what would make us revisit them.
Full index with one-line summaries: [architecture/decisions/](architecture/decisions/README.md).

- [0001](architecture/decisions/0001-modular-monolith.md) — Modular monolith, not microservices
- [0002](architecture/decisions/0002-own-fiscal-engine.md) — Build our own AT-certified fiscal engine
- [0003](architecture/decisions/0003-site-agent.md) — Introduce an in-restaurant Site Agent
- [0004](architecture/decisions/0004-react-pwa-not-blazor.md) — React PWA for clients, not Blazor
- [0005](architecture/decisions/0005-plain-guid-ids.md) — Plain `Guid` ids, isolation enforced by RLS
- [0006](architecture/decisions/0006-no-mediatr.md) — Hand-rolled dispatcher instead of MediatR
- [0007](architecture/decisions/0007-client-agnostic-api.md) — One client-agnostic API for every platform
- [0008](architecture/decisions/0008-token-auth-no-cookies.md) — Token auth, device-bound refresh, no cookies

## Fiscal domain

The highest-risk area of the system. Read before touching anything under
`Brasa.Fiscal.*`.

- [fiscal/README.md](fiscal/README.md) — Portuguese requirements: ATCUD, QR, signature, SAF-T
- [fiscal/certification.md](fiscal/certification.md) — the AT certification process and its prerequisites
- [fiscal/key-management.md](fiscal/key-management.md) — custody of the signing key

## Development

- [development/getting-started.md](development/getting-started.md)
- [development/testing.md](development/testing.md) — the testing bar, which is not uniform
- [development/e2e-testing.md](development/e2e-testing.md) — end-to-end strategy (next up)
- [development/documentation.md](development/documentation.md) — how docs are kept current

## Product

- [product/backlog.md](product/backlog.md) — **every feature and task, with status. The plan of record**
- [product/differentiation.md](product/differentiation.md) — competitive positioning and what makes this worth starting
- [product/status.md](product/status.md) — which code actually exists today
- [product/plan.md](product/plan.md) — the approved build plan and 6-month roadmap
