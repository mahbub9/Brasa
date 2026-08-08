# ADR 0003 — Introduce an in-restaurant Site Agent

**Status:** Accepted · **Date:** 2026-08-08

## Context

Three requirements collide in a browser-based POS:

1. **Offline service.** A restaurant's internet will fail mid-service. Taking
   orders and payments cannot stop.
2. **Offline fiscal signing.** Portuguese documents need a chained RSA signature
   over a gapless per-series sequence. A private key cannot live safely in a
   browser, and a cloud round trip is unavailable precisely when it is needed.
3. **Thermal printing.** A PWA cannot open raw TCP sockets, so it cannot drive
   ESC/POS kitchen printers or a cash drawer.

Alternatives considered:

| Option | Why rejected |
|---|---|
| Cloud-only signing | Payment stops during any outage. Unacceptable for a POS |
| Cloud-connected printers (Star CloudPRNT, Epson Server Direct Print) | Solves printing only, still needs internet, locks customers to specific printer models |
| Browser + WebUSB | Poor cross-device support, hostile permission UX, no key custody story |
| Contingency/manual invoicing during outages | Legally provided for, but a terrible product |

## Decision

Run a **.NET worker process inside each restaurant** — on a mini-PC or the main
POS terminal. It owns:

1. Custody of the fiscal RSA private key, and offline document signing
2. Gapless series numbering
3. ESC/POS printing to kitchen and bar stations
4. A LAN REST + SignalR hub that terminals and KDS connect to
5. An outbox that syncs to the cloud when connectivity returns

Each **site** (later, each terminal) owns its own document series, registered
independently with AT. The cloud is a **replica and the SAF-T submitter**; it
never re-numbers and never re-signs.

## Consequences

**Good**

- Three hard problems collapse into one deployable unit.
- Exactly one signing authority per series — an entire class of reconciliation
  bugs becomes impossible by construction.
- Terminals stay in sync with each other over LAN even with the internet down.
- It is written in C#, the founder's strongest language, and is the majority of
  the genuinely difficult code.

**Bad**

- Something must be installed and updated in every restaurant. Pairing,
  auto-update and remote diagnostics all become our problem.
- The Site Agent falls within the scope of AT certification, since it is what
  signs.
- Estimated +6–8 weeks versus a cloud-only design.

## Notes

QR self-ordering is the one flow that necessarily traverses the cloud: a guest's
phone reaches the internet, and the cloud pushes the order down to the agent. In
MVP, **payment stays with staff at the table**, which preserves the
single-signing-authority rule. Online guest payment would introduce a second
issuing path and is deliberately deferred.

## Revisit when

- Deployment or support burden proves worse than modelled at ~20+ sites.
- A future AT ruling constrains where signing may occur.
