# Pre-bill: a documento não fiscal, not an invoice

> **Status:** ✅ built
> **Module:** Ordering (composes Fiscal as a calculator, never as a document issuer)
> **Roadmap:** I2

## What it is

A table can see an itemised preview of what it currently owes — every line,
a VAT breakdown by rate band, and a total — before anyone pays anything.
`GET /orders/{id}/pre-bill` computes this fresh from the order's current
lines every time it's called. It is explicitly **not** an invoice: no
document number, no ATCUD, no QR code, nothing issued or numbered. Asking
to see the bill twice, or ten times, produces identical figures each time —
a "reprint" that costs nothing because nothing was ever recorded.

## Why it works this way

**Issuing it as a fiscal document would fiscalise every table that merely
asks to see the bill.** Portuguese fiscal law numbers and chains real
invoices; a party asking "how much do we owe so far" mid-meal, possibly
several times before they actually pay, must never advance a fiscal
sequence or produce a document AT could later expect to reconcile. See
[docs/fiscal/README.md](../fiscal/README.md).

**`FiscalDocumentLine` is reused purely as a calculator.** The pre-bill
needs the exact same gross-inclusive net/VAT derivation a real fiscal
document will eventually use — same rounding, same per-band grouping — so
`GetPreBillAsync` builds the same `FiscalDocumentLine` list `CloseOrderAsync`
would (`BuildFiscalLines`), but never calls `IFiscalProvider` with them.
Nothing is signed, sequenced, or persisted; the lines exist only for the
duration of building the response. This is what makes a reprint free and
exact: there is no stored artefact to drift from, only the order's own
current state re-derived on demand.

**The DTO shape enforces the distinction, not just the code path.**
`PreBillDto` has no `documentNumber`, `atcud` or `qrPayload` field at
all — a client rendering this response literally cannot mistake it for an
invoice, because the fields an invoice needs aren't there to render. A
fixed `documentKind: "documento_nao_fiscal"` discriminator lets `pos` show
an explicit non-fiscal notice rather than relying on the absence of fields
alone to communicate that.

**Guarded the same way `AddLine` already is, not a new guard shape.**
`Order.EnsureCanGeneratePreBill` rejects a closed order (`order.not_open`)
and an order with no lines yet (`order.empty`) — the same two checks
`AddLine`'s own callers already reason about, reused rather than
reinvented. An order with only voided lines still generates a pre-bill
(a `0,00` one) since voided lines aren't excluded from the line count the
same way they're excluded from the fiscal document's own totals — a
guest who voided everything they ordered should still be able to see that
reflected, not get a `400` for having "no lines."

## Behaviour

1. A table has at least one line on its open order.
2. Staff (or a guest-facing screen, not built yet) requests the pre-bill:
   `GET /orders/{id}/pre-bill`.
3. The response lists every current line (voided lines included, at their
   zeroed contribution — same as the fiscal document), a VAT breakdown
   grouped by rate, the order's current total, and a `generatedAtUtc`
   timestamp.
4. Asking again at any point returns the same figures for the same order
   state — only `generatedAtUtc` can differ between two calls.
5. `pos`'s "Ver conta" button opens a dialog showing the same data, with an
   explicit "Documento não fiscal — não serve de fatura" notice so nobody
   mistakes it for a receipt.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| Pre-bill requested for an order with no lines | Rejected | `400 order.empty` |
| Pre-bill requested for a closed (or merged) order | Rejected | `409 order.not_open` |
| Pre-bill requested for an unknown order | Rejected | `404 order.not_found` |

## Data

Reads only — no new entity, no new column. Computed from the same `Order`/
`OrderLine` rows every other Ordering endpoint already reads, run through
Fiscal's `FiscalDocumentLine` value objects (not persisted).

## API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/orders/{orderId}/pre-bill` | Preview the current bill for an open order |

A plain read — no `Idempotency-Key`, since nothing is mutated and calling
it any number of times is by design indistinguishable from calling it once.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None issued — that's the entire point of this feature. See
[docs/fiscal/README.md](../fiscal/README.md) for why a pre-bill and a real
fiscal document (`FS`/`FT`/`FR`) must never be the same code path.

## Permissions

None enforced — same "ships ahead of manager authorisation" shape most
other Ordering mutations in this codebase have today, though this
particular endpoint is read-only and touches no money.

## Testing

`pre-bill.spec.ts` — a real two-rate-band order (13% food + 23% alcohol)
produces a correct per-band VAT breakdown that reconciles to the order
total; the wire response has no `documentNumber`/`atcud`/`qrPayload` field
at all, confirmed by inspecting the raw JSON, not just the typed DTO;
requesting it twice reproduces identical `total`/`lines`/`vatBreakdown`,
only `generatedAtUtc` differing; an empty order and a closed order are both
rejected with their own codes; the `pos` "Ver conta" dialog shows the
non-fiscal notice and the correct total through a real browser, passes a
WCAG A/AA scan as a modal dialog, and a close-then-reopen reproduces the
same total (the UI's own reprint path).

## Open questions

- No guest-facing surface reads this yet — only `pos`'s staff-facing "Ver
  conta" button does. A future QR self-ordering client (the `order` shell,
  post-I8) is the more natural long-term consumer of a guest wanting to
  check the running total themselves.
