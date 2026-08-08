# AT certification

> **Status: not started.** No application has been submitted. This page is the
> reference for when it is — Month 6 on the roadmap, with prerequisites that must
> start far earlier.

## The essential point

**Certification is granted to the software *producer*, not the customer.** It
gates our ability to *sell*, not our ability to *build*.

We can legally build, test, demo and pilot uncertified. What we cannot do is let a
real restaurant issue real fiscal documents with it.

## Prerequisites — start these early

| # | Prerequisite | Status | Note |
|---|---|---|---|
| 1 | Portuguese legal entity (Lda or ENI) with NIF | ❌ **Not formed** | Formation plus tax registration takes weeks and has **no dependency on code** |
| 2 | Regularised tax situation for that entity | ❌ | Follows from 1 |
| 3 | RSA key pair, public half communicated to AT | ❌ | See [key-management.md](key-management.md) |
| 4 | Working engine meeting the technical requirements | ❌ | Month 4 |

> ⚠️ **Blocker 1 is on the critical path to revenue.** Leaving entity formation
> until Month 6 would mean idling at the finish line with finished software and no
> way to apply. It is tracked in
> [../product/status.md](../product/status.md).

## The process

1. Build the engine and validate it against **AT's test environment**.
2. Submit **Modelo 24** (per Declaração n.º 169/2010) to AT.
3. AT reviews within **30 days**.
4. AT may run **conformity tests**. If it does, the applicant is notified and
   **the 30-day clock is suspended** until testing concludes.
5. Remediate findings. Assume at least one cycle.
6. AT issues a **certificate number**, which must then be printed on every
   document the software produces.

Submit at the **start** of the month you have budgeted, not the end.

## What AT verifies

Per Portaria 363/2010, certification depends on cumulative verification that the
software:

- exports SAF-T (PT) in the required format
- identifies invoices and corrective documents using **asymmetric cryptography
  with a private key**
- has **no function permitting alteration of fiscal information without leaving
  evidence**
- maintains sequential numbering integrity with no gaps
- generates ATCUD correctly from registered series
- includes a conformant QR code on all documents

That third point is worth reading twice. It is not "do not alter fiscal data" —
it is "the software must not *be capable* of altering it invisibly". Any admin
tool, any support script, any `UPDATE` path against an issued document is a
certification failure, not merely a bad practice.

## Fees

The gov.pt service page does not publish a fee for the certification request.
**Confirm directly with AT before budgeting** — do not rely on this page or on
third-party summaries.

## Ongoing obligations

- Certification covers a **specific software version**. Behavioural changes to
  fiscal logic are certification-relevant.
- The certificate number appears on every document.
- The list of certified programs is public, and AT updates it as applications are
  approved.

## Escape hatch

If certification is rejected repeatedly for reasons we cannot engineer around,
`IFiscalProvider` allows a certified partner adapter to be dropped in behind the
existing interface. That is a contained fallback, not a rewrite — which is a large
part of why the abstraction exists. See
[ADR 0002](../architecture/decisions/0002-own-fiscal-engine.md).

## Sources

- [Pedir certificação de programa de faturação — gov.pt](https://www.gov.pt/servicos/programa-de-faturacao-certificacao)
- [Consultar estado da certificação](https://www.gov.pt/servicos/consultar-estado-da-certificacao-de-programa-de-faturacao)
- [Portaria n.º 363/2010 (consolidated)](https://diariodarepublica.pt/dr/legislacao-consolidada/portaria/2010-119668497)

Take the authoritative technical specifications from the Portal das Finanças.
