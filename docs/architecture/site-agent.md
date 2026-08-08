# Site Agent

> **Status: stub.** `Brasa.SiteAgent` starts and stops cleanly and does
> nothing else. Everything below is the design, scheduled for Month 3.
> See [../product/status.md](../product/status.md).

A .NET worker running **inside each restaurant**, on a mini-PC or the main POS
terminal. Its existence is the most consequential decision in the system — see
[ADR 0003](decisions/0003-site-agent.md) for why the alternatives were rejected.

## Responsibilities

### 1. Fiscal signing and series numbering

Holds the RSA private key. Assigns gapless sequential numbers and computes the
chained signature for its site's document series.

**This is why the POS can take payment during an internet outage.** Portuguese
law permits multiple series, each registered independently with AT, so a site
owning its own series can issue fully compliant documents with no connectivity.

The cloud never re-numbers and never re-signs. One signing authority per series.

### 2. ESC/POS printing

Drives kitchen and bar thermal printers over TCP or USB, with station routing,
retry, and printer-down fallback (reroute to another station, and surface the
failure on the POS rather than silently dropping a ticket).

A browser cannot open raw TCP sockets, which is why this cannot live in the PWA.

### 3. LAN hub

Serves a REST API and a SignalR hub on the restaurant's local network. The POS
and KDS clients connect **to the agent first** and fall back to the cloud.

This keeps multiple terminals in sync with each other even when the site is cut
off from the internet entirely.

### 4. Cloud sync

An outbox pushes local changes to the cloud, and a delta pull refreshes menu,
pricing, staff and configuration. Idempotency keys make retries safe.

## Local storage

SQLite via EF Core — the same ORM and much of the same model code as the cloud.

## Topology

```
        ┌──────────── CLOUD ────────────┐
        │   API · PostgreSQL · SAF-T    │
        └───────▲───────────────┬───────┘
      outbox sync │             │ config pull
        ┌─────────┴─────────────▼───────┐
        │          SITE AGENT           │
        │  SQLite · RSA key · ESC/POS   │
        │      LAN REST + SignalR       │
        └──▲──────────▲──────────▲──────┘
           │ LAN      │ LAN      │ LAN
        POS PWA    KDS PWA    (QR order via cloud)
```

## Certification scope

Because the Site Agent is what signs, **it falls within the scope of AT
certification**. Changes to its signing, numbering or document-generation
behaviour are certification-relevant. See
[../fiscal/certification.md](../fiscal/certification.md).

## Open questions

Recorded here so they are not rediscovered later:

- **Deployment**: Windows Service, or a container on a small Linux box? A
  container is cleaner to update; a service is easier to support on hardware the
  restaurant already owns.
- **Auto-update**: certification-relevant changes cannot be silently pushed.
  Needs a version-pinning and rollout story.
- **Hardware shortlist**: MVP should support a documented, tested set of ESC/POS
  models rather than claiming universal support.
- **Agent-down fallback**: if the mini-PC dies mid-service, what happens? Likely
  a degraded cloud-signing mode using a separate reserve series.
