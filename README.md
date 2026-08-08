# Brasa

> *brasa* — the glowing embers of a grill. The heat a Portuguese kitchen is built
> around, as in *frango na brasa*.

Multi-tenant restaurant management SaaS for the Portuguese market — point of sale,
kitchen display, kitchen printing, and QR self-ordering, built around an
AT-certifiable fiscal engine.

> **Status: early foundations.** The solution structure, shared kernel, and build
> pipeline exist. Most modules are empty projects. See
> [docs/product/status.md](docs/product/status.md) for exactly what is and is not
> built — do not assume a project does anything just because it exists.

---

## Why this is not a generic POS

Portuguese law requires invoicing software to be certified by the *Autoridade
Tributária* (AT) under Portaria 363/2010. Non-certified software carries fines of
€3,000–€18,750 per infraction, and the requirement applies to essentially every
restaurant worth selling to. That single constraint drives most of the
architecture:

- Fiscal documents need a **chained RSA signature** over a gapless per-series
  sequence, so signing must work **offline** — a restaurant's internet will fail
  mid-service and service cannot stop.
- A browser cannot hold a private key safely, nor open raw TCP sockets to drive
  ESC/POS kitchen printers.

Both are solved by the **Site Agent**, a .NET process running inside each
restaurant. See [docs/architecture/README.md](docs/architecture/README.md).

## Repository layout

```
src/backend/
  Brasa.Api                 ASP.NET Core cloud API (/api/v1)
  Brasa.Shared              Shared kernel — Money, Result, tenancy, outbox
  Brasa.Modules.*           Identity, Catalog, Ordering, Fiscal, Payments, Reporting
  Brasa.Fiscal.Portugal     ATCUD, RSA chain, QR, SAF-T (PT), AT webservices
  Brasa.Fiscal.Mock         Deterministic fake for development and tests
src/agent/
  Brasa.SiteAgent           In-restaurant worker: signing, printing, LAN hub
tests/                              Unit, fiscal golden-file, and integration tests
docs/                               Architecture, fiscal domain, decisions, roadmap
infra/                              Local development infrastructure
```

## Getting started

Prerequisites, setup, and the current local-development gaps are in
**[docs/development/getting-started.md](docs/development/getting-started.md)**.

The short version:

```bash
dotnet build Brasa.slnx
dotnet test  Brasa.slnx
```

## Documentation

Start at **[docs/README.md](docs/README.md)**. Documentation is maintained
alongside the code, not after it — see the contract in
[docs/development/documentation.md](docs/development/documentation.md).

## Licence

Proprietary. All rights reserved.
