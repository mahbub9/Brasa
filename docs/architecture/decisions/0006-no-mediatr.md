# ADR 0006 — Hand-rolled event dispatcher instead of MediatR

**Status:** Accepted · **Date:** 2026-08-08

## Context

The original plan named MediatR as the in-process integration-event dispatcher.
MediatR is the default choice for this pattern in .NET.

MediatR moved to a **commercial licence** for versions beyond the Apache-2.0 era.
This product is a commercial SaaS, so a paid dependency in the core message path
is a recurring cost and a licence-compliance obligation for a piece of
infrastructure we need perhaps 150 lines of.

## Decision

Define the contracts ourselves in `Brasa.Shared.Messaging`:

- `IIntegrationEvent` / `IntegrationEvent`
- `IIntegrationEventHandler<TEvent>`
- `IIntegrationEventDispatcher`
- `OutboxMessage`

Dispatch resolves handlers from the DI container by closed generic type.

## Consequences

**Good**

- No licence obligation or cost on the core path.
- The contracts are ours, so the outbox-to-broker transition later is a change we
  fully control.
- Dramatically less machinery than MediatR's full pipeline, most of which this
  system does not use.

**Bad**

- We own it, including behaviours MediatR gives free (pipeline behaviours,
  notification publishing strategies).
- Contributors familiar with MediatR must learn a local convention.

## Notes

We are deliberately **not** reimplementing MediatR. There is no request/response
mediator and no pipeline. Modules expose ordinary interfaces to the API layer;
`IIntegrationEventDispatcher` exists solely for cross-module facts flowing
through the outbox.

## Revisit when

- We need pipeline behaviours (retry, validation, transaction scoping) across
  many handlers and the hand-rolled version starts growing one.
