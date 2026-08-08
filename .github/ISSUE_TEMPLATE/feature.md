---
name: Feature
about: New capability or an enhancement
title: '[FEAT] '
labels: enhancement
---

## Problem

<!-- What a restaurant cannot do today, and what it costs them. -->

## Proposed behaviour

## Module

<!-- One module owns this. If it seems to need two, say why — that usually means
     an integration event, not a shared table.
     See docs/architecture/module-boundaries.md -->

- [ ] Identity
- [ ] Catalog
- [ ] Ordering
- [ ] Fiscal
- [ ] Payments
- [ ] Reporting
- [ ] Site Agent
- [ ] Web client

## Offline behaviour

<!--
Required for anything on the service path. What happens when the internet is
down? What happens when the Site Agent is unreachable?
"Not applicable" is a valid answer for back-office features — say so explicitly.
-->

## Roadmap fit

<!-- See docs/product/plan.md. Which phase? If it is not in the plan, say what
     it displaces — the six-month schedule has no slack. -->

## Documentation to write

- [ ] Feature doc under `docs/features/`
- [ ] ADR, if a contested technical choice is involved
- [ ] `docs/product/status.md` entry
