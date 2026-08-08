# Portuguese fiscalisation

> **Read this before changing anything under `Brasa.Fiscal.*` or
> `Brasa.Modules.Fiscal`.** This is the highest-risk area of the system.
> Mistakes here are not bugs — they are fines for our customers and a revoked
> certificate for us.

## Why it exists

Invoicing software used in Portugal must be **certified in advance by the
Autoridade Tributária e Aduaneira (AT)** under Portaria n.º 363/2010 (regulating
art. 123 of the CIRC), as amended by Portaria n.º 340/2013 and later instruments.

Certified software is mandatory for taxpayers who:

- had turnover above **€50,000** in the previous year, **or**
- issue more than **1,000 invoices** per year, **or**
- have organised accounting, **or**
- are non-residents registered for VAT in Portugal.

That covers effectively every restaurant we want as a customer.

**Penalties:** using non-certified software carries fines of **€3,000–€18,750**
per infraction. A missing or malformed QR code is **€200–€1,000 per invoice**.

**Certification is granted to the software producer, not the customer.** It gates
our ability to *sell*, not our ability to *build*. See
[certification.md](certification.md).

## The four pillars

Every fiscal document we issue must satisfy all four. They are not independent —
the ATCUD depends on series registration, and the signature depends on the
sequence.

### 1. Gapless sequential numbering, per series

Document numbers within a series are strictly sequential with **no gaps**. A gap
is evidence of a deleted document and is exactly what certification exists to
prevent.

Each series is registered with AT before use and belongs to exactly one issuing
authority. In our design that authority is the **Site Agent** — see
[../architecture/decisions/0003-site-agent.md](../architecture/decisions/0003-site-agent.md).

### 2. Chained RSA signature

Each document is signed with an RSA private key whose public half is registered
with AT. The signed string is:

```
{InvoiceDate};{SystemEntryDate};{InvoiceNo};{GrossTotal};{PreviousDocumentHash}
```

The result is Base64-encoded and stored **in full**. The printed document shows
only 4 characters — those at positions 1, 11, 21 and 31 of the Base64 string.

`PreviousDocumentHash` is the full hash of the previous document **in the same
series**, which is what makes the chain tamper-evident: altering any document
invalidates every document after it.

> Chaining is per-series, never global. Two series advance independently.

### 3. ATCUD

`ATCUD = {SeriesValidationCode}-{SequentialNumber}`

The validation code is obtained by **registering the series with AT via
webservice before issuing against it**. It cannot be computed locally. A series
that has not been registered cannot legally issue documents — which is why series
registration is a provisioning step, not a runtime one.

### 4. QR code

A pipe-delimited field string encoding issuer NIF, customer NIF, document type,
status, dates, totals per VAT rate, ATCUD, and the 4-character signature excerpt.
Rendered at **minimum 30×30 mm**.

## SAF-T (PT)

An XML export validated against AT's published XSD, covering `MasterFiles` and
`SourceDocuments`.

- **Invoicing SAF-T**: submitted monthly, **by the 5th of the following month**.
- **Accounting SAF-T**: deferred by the 2026 State Budget — applicable to 2027
  periods, delivered in 2028 as part of the IES. Not MVP scope, but the data model
  should not make it impossible.

Submission is automated via Hangfire with retry and alerting. A failed submission
must page a human; silently missing the 5th is a customer-facing compliance
failure.

## Document types

| Code | Name | Use |
|---|---|---|
| `FS` | Fatura simplificada | The restaurant workhorse. Permitted to final consumers up to €1,000 |
| `FT` | Fatura | When the customer gives a NIF and wants a full invoice |
| `FR` | Fatura-recibo | Invoice and receipt combined |
| `NC` | Nota de crédito | **The only way to correct an issued document** |

### The pre-bill is not an invoice

The bill handed to a table before payment (*a conta*) is a **documento não
fiscal**. It must be generated as such and clearly labelled. Issuing it as an
invoice would fiscalise every table that asks to see the bill before deciding.

## Immutability

**No code path may update or delete an issued fiscal document.** Corrections
happen through credit notes, never through mutation. This is enforced by:

- An append-only audit log
- A chain-verification job that re-walks each series and alarms on any break
- Golden-file tests asserting byte-identical signature, ATCUD and QR output

If you find yourself writing an `UPDATE` against an issued document, stop.

## VAT

**Do not hardcode rates.** Rates are modelled as `TaxRule`, keyed by
item × channel (dine-in / takeaway / delivery) × region (Continental / Madeira /
Açores), each with an effective date range.

As of 2026 the headline restaurant rates on the mainland are:

| Rate | Applies to |
|---|---|
| 13% (intermédia) | Meals and non-alcoholic drinks |
| 23% (normal) | Alcoholic drinks |
| 6% (reduzida) | Certain items; accommodation |

Madeira and the Azores use different rate bands. Invoices must **separate
alcoholic drinks from food**, since they attract different rates.

> ⚠️ **These rates are a starting point, not a source of truth.** Rates change,
> takeaway treatment has shifted historically, and there is active political
> debate about the 13% band. **Have a Portuguese accountant confirm current rules
> before launch.** The data model absorbs whatever answer they give.

## Sources

- [Portaria n.º 363/2010 (consolidated)](https://diariodarepublica.pt/dr/legislacao-consolidada/portaria/2010-119668497)
- [Portaria n.º 340/2013](https://diariodarepublica.pt/dr/detalhe/portaria/340-2013-503842)
- [Pedir certificação de programa de faturação — gov.pt](https://www.gov.pt/servicos/programa-de-faturacao-certificacao)
- [ATCUD, SAF-T and QES in Portugal (2026) — fiskaly](https://www.fiskaly.com/blog/fiscalization-atcud-qes-in-portugal)
- [Portugal's E-Invoicing Rules — VATupdate](https://www.vatupdate.com/2026/05/29/portugals-e-invoicing-rules-certified-software-atcud-qr-codes-and-saf-t/)

Official AT specifications (QR field layout, SAF-T XSD, webservice WSDLs) must be
taken from the Portal das Finanças, not from summaries. Third-party articles are
useful orientation and nothing more.
