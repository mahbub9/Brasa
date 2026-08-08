# Money

**Type:** [`Brasa.Shared.Primitives.Money`](../../src/backend/Brasa.Shared/Primitives/Money.cs)
**Tests:** [`MoneyTests`](../../tests/Brasa.Shared.Tests/Primitives/MoneyTests.cs)

## The rule

> **Never use `double`, `float`, or bare `decimal` for a monetary amount.**
> Use `Money`.

`Money` stores a signed **integer count of minor units** — cents, for EUR.

## Why

A POS adds thousands of small amounts per service, and the totals on a fiscal
document must reconcile **to the cent** against both the SAF-T export and the Z
report. Binary floating point cannot promise that. `decimal` is exact for
decimal fractions but still invites division, which is where cents go missing.

`default(Money)` is zero euros, so the type is safe as an uninitialised field, in
arrays, and as an EF Core owned-type default.

## Splitting a bill: allocation, not division

This is the reason the type exists.

Splitting €10.00 three ways by dividing gives €3.33 each — €9.99 total. One cent
has vanished, the Z report no longer reconciles, and the SAF-T export is wrong.

```csharp
var bill   = Money.FromDecimal(10.00m);
var shares = bill.Allocate(3);        // 3.34 / 3.33 / 3.33
Money.Sum(shares) == bill;            // always true
```

Uneven splits use weights — by covers, or by the value each guest ordered:

```csharp
var shares = bill.Allocate([3, 2, 2, 1]);
```

Guarantees:

- Shares always sum back to **exactly** the original amount.
- The remainder is distributed one minor unit at a time, in index order, so the
  split is **deterministic** — a reprinted receipt matches the original.
- Refunds allocate identically to the sale they reverse, so a credit note cancels
  its invoice to exactly zero.

`MoneyTests.Allocation_always_sums_back_to_the_original` checks every cent value
from 0 to €20.00 across six split counts — roughly 12,000 combinations.

## Rounding

Rounding is **half away from zero** (`MidpointRounding.AwayFromZero`), the
convention Portuguese fiscal documents use. It is applied at exactly two places:

- `Money.FromDecimal` — converting external input at the boundary
- `Money * decimal` — VAT and percentage discounts

Both re-quantise to whole minor units immediately, so rounding error cannot
accumulate across order lines.

```csharp
var net = Money.FromDecimal(12.50m);
var vat = net * 0.13m;   // 162.5 -> 163 cents
```

## Formatting

| Call | Output | Use |
|---|---|---|
| `ToString()` | `1250 EUR` | Logs, diagnostics. Invariant |
| `Format(culture)` | `12,50 €` in pt-PT | Anything a person reads |

The display method is deliberately **not** called `ToString`. Keeping them
distinct means the parameterless form is unambiguously the invariant diagnostic
form, and a caller wanting customer-facing output must name the culture.

## Currency

`CurrencyCode` is an enum with `Eur = 0`. Portugal is euro-only, so every amount
in the system today is EUR. The type exists so that a non-euro market later is an
additive change rather than a migration of every money column.

Combining amounts of different currencies throws rather than silently converting.
