# ADR 0004 — React PWA for clients, not Blazor

**Status:** Accepted · **Date:** 2026-08-08

## Context

The backend is C#, and the developer is strongest in C#. Blazor WebAssembly would
allow one language across the whole stack, which is a real and tempting benefit
for a solo developer.

The POS client, however, has unusual constraints:

- Must **boot in under ~2 seconds** on a cheap Android tablet, cold.
- Must run **fully offline**, with a local store and a sync outbox.
- Is a **touch-first, highly interactive** UI: floor-plan editing, virtualised
  menu grids, split-bill interactions.

## Decision

Build `pos`, `kds`, `admin` and `order` as **React + TypeScript + Vite** PWAs,
sharing a component library (`web/ui`) and an OpenAPI-generated client
(`web/sdk`).

## Consequences

**Good**

- Mature offline tooling: service workers, and Dexie over IndexedDB for the local
  store and outbox.
- Small initial payload and fast cold start, which is the single most
  user-visible quality of a POS.
- Deep ecosystem for touch UI, drag-and-drop and virtualised lists.
- Deployment stays trivial: static files behind Caddy or a CDN. No Node server.

**Bad**

- Two languages and two toolchains in one repository.
- Types are defined twice unless the SDK is generated — hence `web/sdk` is
  generated from the API's OpenAPI document, not hand-written.

## Rationale for the trade

The C# strength is spent where it counts: the API, the fiscal engine, and the
Site Agent — which together are the majority of the difficult code. The front-end
is unavoidably web regardless of language choice, so the question is only which
web toolchain, and for *this* workload React is materially ahead.

## Revisit when

- Blazor WASM cold-start and payload size become competitive with a Vite bundle
  on low-end Android hardware.
- The web clients grow complex enough that duplicated domain logic across
  languages becomes a genuine source of bugs.
