# Effective-dated tax rules

> **Status:** 🚧 in progress — the model and resolution service are built and verified live; nothing in ordering resolves through it yet
> **Module:** Catalog
> **Roadmap:** I1

## What it is

A `TaxRule` records the VAT rate that applies to a category of sale —
alcoholic or not, dine-in or takeaway, in a given Portuguese region —
during an effective date range. It replaces the hardcoded-constant
instinct `VatRate` (the flat rate a `MenuItem` still carries today)
always named itself as a temporary placeholder for.

## Why it works this way

**"Item" is the alcohol band, not a per-item key.** This feature's own
backlog title reads "`TaxRule` — item × channel × region," which could
suggest a rule keyed by individual `MenuItemId`. Portuguese VAT law
taxes categories of goods and the channel they're sold through, never
one named product by name — the same reason `MenuItem.IsAlcoholic`
(CAT-09) exists at all rather than a per-item rate field. `IsAlcoholic`
is therefore the whole "item" dimension: two bands, not one row per
menu item.

**Create-only, no update or delete.** A correction to a rate is a new,
later-effective row, never an edit to one already on file — the same
"never mutate, only add" instinct fiscal documents themselves follow,
applied here even though a `TaxRule` is not itself a fiscal document.
This makes the historical question "what rate applied to a sale made
on 2026-03-01" always answerable from the data as it stood, rather than
overwritten by a later correction.

**Resolution, not storage, is the actual point.** `TaxRule.Resolve`
picks the rule in force for a combination at a given instant — the
most recently-*started* rule wins if two rules' ranges ever wrongly
overlap for the same combination, a data-entry mistake this codebase
chooses to resolve deterministically rather than reject outright.

**Deliberately not wired into `AddLine`, `AddComboLineAsync`, or the
fiscal document builder — the biggest named gap this feature ships
with.** All three still read `MenuItem.VatRate` directly. Rewiring the
live VAT computation path to resolve through `TaxRule` instead touches
the most fiscal-sensitive code in the entire system — every guest's
bill and every fiscal document's VAT breakdown depends on getting it
right — and doing that safely deserves its own dedicated, carefully
verified pass, not a side effect of shipping the data model. Same
"mechanism before the trigger" shape CAT-05 (price lists), CAT-10
(combos), CAT-16 (scheduled prices) and FLR-05 (table groups) each
already used for a genuinely separate reason each time (no
site-selection concept, no combo UI, no client that reads a scheduled
price, no floor-plan multi-select) — here the reason is fiscal risk,
not a missing client feature.

**Rates stay data, never a hardcoded constant.** Dev-seeded rows use
the same two rates `VatRate.IntermediateMainland`/`StandardMainland`
already name (13%/23%, mainland, both channels), effective from a
fixed 2024-01-01 anchor rather than "now" — so resolving "today" always
finds a rule regardless of which day the seeder happens to run. Current
rates remain unconfirmed by an accountant; see
[../fiscal/README.md](../fiscal/README.md). Madeira/Azores rows are
deliberately not seeded — no seeded site claims either region yet, and
inventing rates nobody asked for would be worse than an honest gap.

## Behaviour

1. An owner or accountant adds a rate: `POST /tax-rules` with
   `{ isAlcoholic, isTakeaway, region, vatRatePercent, effectiveFromUtc, effectiveToUtc? }`.
2. Anyone resolves the rate in force right now, or at a specific
   instant: `GET /tax-rules/resolve?isAlcoholic=&isTakeaway=&region=&atUtc=`
   (`atUtc` optional, defaults to the current instant).
3. `GET /tax-rules` lists every rule on file, for review.
4. Correcting a rate means adding a new row with a later
   `effectiveFromUtc`, never editing or deleting the old one.
5. `AddLine`, `AddComboLineAsync` and the fiscal document builder do
   **not** call any of this yet — they still resolve VAT from
   `MenuItem.VatRate` directly, unchanged by this feature's existence.

## Offline behaviour

Not applicable — cloud API endpoints, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| `region` not a recognised `PortugueseRegion` name | Rejected, on create or resolve | `400 catalog.invalid_tax_rule_region` |
| `vatRatePercent` outside 0–1 (a fraction, not a whole-number percentage) | Rejected | `400 catalog.invalid_vat_rate_percent` |
| `effectiveFromUtc`/`effectiveToUtc` not a valid date/time | Rejected | `400 catalog.invalid_tax_rule_date` |
| `effectiveToUtc` not strictly after `effectiveFromUtc` | Rejected | `400 catalog.invalid_tax_rule_effective_range` |
| Resolving a combination with no rule in force at the resolved instant | Rejected | `404 catalog.tax_rule_not_found` |

## Data

`TaxRule` (`IsAlcoholic`, `IsTakeaway`, `Region`, `Rate` — a `VatRate`
value, mapped the same `numeric(4,2)` fraction shape `MenuItem.VatRate`
already uses — `EffectiveFromUtc`, `EffectiveToUtc?`), owned by
Catalog, indexed on `(IsAlcoholic, IsTakeaway, Region)` for the
resolve lookup. A standalone top-level entity, not owned by
`MenuItem` — the same "flat, opaque reference" shape `PriceList`/
`Combo` already established for cross-cutting Catalog concepts.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/tax-rules` | Add a new effective-dated rate |
| `GET` | `/tax-rules` | List every rate on file |
| `GET` | `/tax-rules/resolve` | Resolve the rate in force for a combination at an instant |

`POST` takes `Idempotency-Key` like every other mutation. `vatRatePercent`
is a fraction (`0.13` for 13%), the same convention `MenuItemDto`'s own
field of the same name already uses.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

Not yet — this is infrastructure for a future fiscal-impacting change,
not one itself. `AddLine`/`AddComboLineAsync`/the fiscal document
builder all still resolve VAT from `MenuItem.VatRate`, unchanged.
Rewiring them is named directly as this feature's own biggest open gap
above. Once wired, this becomes directly certification-relevant — see
[../fiscal/README.md](../fiscal/README.md) — since it would decide the
VAT rate printed on every fiscal document.

## Permissions

None enforced yet — same "ships ahead of manager authorisation" shape
every other Catalog mutation in this codebase has today. IDN-11 is the
eventual real gate, once staff accounts and roles exist; entering tax
rates is exactly the kind of action that will eventually need an
owner/accountant-only role, not just any authenticated staff member.

## Testing

`tax-rules.spec.ts` — the seeded mainland rates resolve correctly for
both bands and both channels; on a region only this spec touches, a
later rule supersedes an earlier one exactly within its own window and
nothing resolves before the earliest rule starts; an unrecognised
region, an out-of-range percentage, an unparsable date and a backwards
effective range are all rejected on create; an uncovered combination
404s on resolve too.

## Open questions

- The biggest one: rewiring `AddLine`/`AddComboLineAsync`/the fiscal
  document builder to actually resolve VAT through this instead of the
  flat `MenuItem.VatRate` — named directly above as deliberately
  deferred, fiscally sensitive work.
- No update or delete by design (see "Why it works this way"), but
  there is also no UI anywhere yet for browsing a rule's own history
  once several effective-dated rows pile up for the same combination.
- Current seeded rates (13%/23%) are unconfirmed by an accountant —
  see [../fiscal/README.md](../fiscal/README.md). The data model
  absorbs whatever answer they give; the seeded values do not.
- Madeira/Azores rates are entirely unseeded — no site claims either
  region yet (IDN-01's `Site.Region` exists, but nothing seeds a site
  in either region today).
