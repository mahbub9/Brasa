# ADR 0002 — Build our own AT-certified fiscal engine

**Status:** Accepted · **Date:** 2026-08-08

## Context

Portuguese law requires AT-certified invoicing software. There were three ways to
satisfy it:

1. **Integrate a certified partner API** (InvoiceXpress, Vendus, Moloni,
   KeyInvoice) and let them issue the fiscal documents.
2. **Build our own engine** and apply to AT for certification.
3. **Hybrid** — an abstraction with a partner adapter now, own engine later.

Partner pricing is structured for freelancers, not restaurants. InvoiceXpress
publishes a free tier at **1 document/month** and tiers up to a "custom proposal"
above **1,500 documents/month**. A restaurant issues a *fatura simplificada* per
table: a modest restaurant produces ~1,000–2,000 documents/month, and a busy café
doing 300–500 transactions/day produces **9,000–15,000**. Every customer lands in
negotiated enterprise pricing on day one, permanently, per site.

The decisive factor was that **there is no near-term revenue deadline.** The
founder is not selling immediately.

## Decision

Build our own engine in `RestaurantPos.Fiscal.Portugal`, behind an
`IFiscalProvider` abstraction, with `RestaurantPos.Fiscal.Mock` used for
development and tests.

## Consequences

**Good**

- No per-document cost, ever. The margin on high transaction volume — the whole
  point of the product — stays with us.
- No dependency on a competitor's roadmap. (Note that InvoiceXpress and Moloni
  are both owned by Visma, so "two vendors" is really one.)
- **The certificate is the moat.** It is precisely the barrier that keeps casual
  competitors out of the Portuguese market.
- `IFiscalProvider` is the country seam. Spain (Verifactu/TicketBAI), France
  (NF525) and Italy become new implementations, not forks.

**Bad**

- We own the correctness of signature chaining, ATCUD, QR and SAF-T.
- Certification is a real process with real lead time. AT's formal review is 30
  days, **suspended while conformity tests run**, so at least one remediation
  cycle should be assumed.
- Requires a Portuguese legal entity before we can even apply — certification is
  granted to the *producer*. This is on the critical path and must start early.

## Mitigations

- Mock provider from day one, so POS development never blocks on certification.
- Build against AT's **test environment** from Month 4.
- Accountant review of real generated documents before submitting Modelo 24.
- Pilot restaurants run in **parallel-run mode** — real orders and kitchen flow
  through our system while fiscal documents are still issued by their existing
  certified software. This is legal and needs no certificate.

## Revisit when

- Certification is rejected twice for reasons we cannot engineer around — at
  which point a partner adapter behind the existing `IFiscalProvider` is a
  contained fallback, not a rewrite.
