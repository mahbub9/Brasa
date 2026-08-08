using System.Globalization;

namespace RestaurantPos.Shared.Primitives;

/// <summary>
/// A monetary amount stored as an integer count of minor units (cents for EUR).
/// </summary>
/// <remarks>
/// <para>
/// <b>Money is never represented in floating point anywhere in this system.</b>
/// A restaurant POS adds thousands of small amounts per service, and the totals
/// on a fiscal document must reconcile to the cent against both the SAF-T export
/// and the Z report. Binary floating point cannot guarantee that; an integer
/// count of cents can.
/// </para>
/// <para>
/// <c>default(Money)</c> is zero euros, so the type is safe to use in arrays,
/// as an uninitialised field, and as an EF Core owned-type default.
/// </para>
/// <para>
/// Splitting a bill uses <see cref="Allocate(int)"/> or
/// <see cref="Allocate(ReadOnlySpan{int})"/>, never division. See
/// <c>docs/architecture/money.md</c> for why.
/// </para>
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>Creates an amount from a count of minor units (cents for EUR).</summary>
    public Money(long minorUnits, CurrencyCode currency = CurrencyCode.Eur)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    /// <summary>The amount, as a signed count of minor units (cents for EUR).</summary>
    public long MinorUnits { get; }

    /// <summary>The currency this amount is denominated in.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Zero euros.</summary>
    public static Money Zero => default;

    /// <summary>Zero in the given currency.</summary>
    public static Money ZeroIn(CurrencyCode currency) => new(0, currency);

    /// <summary>True when the amount is exactly zero.</summary>
    public bool IsZero => MinorUnits == 0;

    /// <summary>True when the amount is below zero.</summary>
    public bool IsNegative => MinorUnits < 0;

    /// <summary>True when the amount is above zero.</summary>
    public bool IsPositive => MinorUnits > 0;

    /// <summary>Creates an amount from whole major units, e.g. <c>FromMajor(12)</c> is €12.00.</summary>
    public static Money FromMajor(long majorUnits, CurrencyCode currency = CurrencyCode.Eur)
        => new(majorUnits * currency.MinorUnitsPerMajor(), currency);

    /// <summary>
    /// Converts a decimal amount in major units to <see cref="Money"/>, rounding
    /// half away from zero — the convention Portuguese fiscal documents use.
    /// </summary>
    /// <remarks>
    /// Use this only at the boundary, when parsing input or importing data.
    /// Internal arithmetic should stay in <see cref="Money"/> throughout.
    /// </remarks>
    public static Money FromDecimal(decimal amount, CurrencyCode currency = CurrencyCode.Eur)
    {
        var scaled = amount * currency.MinorUnitsPerMajor();
        return new Money((long)Math.Round(scaled, 0, MidpointRounding.AwayFromZero), currency);
    }

    /// <summary>
    /// The amount in major units as a <see cref="decimal"/>, exact for EUR.
    /// </summary>
    /// <remarks>
    /// For display and for serialising into SAF-T / invoice payloads. Never
    /// round-trip through this for arithmetic.
    /// </remarks>
    public decimal ToDecimal() => (decimal)MinorUnits / Currency.MinorUnitsPerMajor();

    /// <summary>Absolute value of this amount.</summary>
    public Money Abs() => new(Math.Abs(MinorUnits), Currency);

    /// <summary>This amount with its sign flipped.</summary>
    public Money Negate() => new(-MinorUnits, Currency);

    // ── Arithmetic ──────────────────────────────────────────────────────────
    // Operators carry named alternates (Add/Subtract/Multiply) so the type is
    // usable from languages without operator overloading, per CA2225.

    /// <summary>Adds two amounts of the same currency.</summary>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.MinorUnits + right.MinorUnits, left.Currency);
    }

    /// <inheritdoc cref="op_Addition(Money, Money)"/>
    public static Money Add(Money left, Money right) => left + right;

    /// <summary>Subtracts two amounts of the same currency.</summary>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.MinorUnits - right.MinorUnits, left.Currency);
    }

    /// <inheritdoc cref="op_Subtraction(Money, Money)"/>
    public static Money Subtract(Money left, Money right) => left - right;

    /// <summary>Negates an amount.</summary>
    public static Money operator -(Money value) => value.Negate();

    /// <summary>Multiplies an amount by a whole quantity — the line-total case.</summary>
    public static Money operator *(Money value, int quantity)
        => new(value.MinorUnits * quantity, value.Currency);

    /// <inheritdoc cref="op_Multiply(Money, int)"/>
    public static Money Multiply(Money value, int quantity) => value * quantity;

    /// <summary>
    /// Multiplies by a decimal factor — the VAT and discount case — rounding half
    /// away from zero.
    /// </summary>
    /// <remarks>
    /// The intermediate is computed in <see cref="decimal"/>, which is exact for
    /// the rates involved (0.13, 0.23, 0.06). The result is re-quantised to whole
    /// minor units immediately, so rounding error cannot accumulate across lines.
    /// </remarks>
    public static Money operator *(Money value, decimal factor)
    {
        var scaled = value.MinorUnits * factor;
        return new Money((long)Math.Round(scaled, 0, MidpointRounding.AwayFromZero), value.Currency);
    }

    /// <inheritdoc cref="op_Multiply(Money, decimal)"/>
    public static Money Multiply(Money value, decimal factor) => value * factor;

    /// <summary>Sums a sequence, returning <see cref="Zero"/> when it is empty.</summary>
    /// <exception cref="InvalidOperationException">The sequence mixes currencies.</exception>
    public static Money Sum(IEnumerable<Money> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        var total = Zero;
        var seeded = false;
        foreach (var amount in amounts)
        {
            if (!seeded)
            {
                total = amount;
                seeded = true;
                continue;
            }

            total += amount;
        }

        return total;
    }

    // ── Allocation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Splits this amount into <paramref name="parts"/> shares that sum back to
    /// exactly this amount, distributing any indivisible remainder one minor unit
    /// at a time across the leading shares.
    /// </summary>
    /// <remarks>
    /// Splitting €10.00 three ways yields 3.34 / 3.33 / 3.33 — never 3.33 × 3,
    /// which would lose a cent and break the Z report reconciliation.
    /// </remarks>
    public Money[] Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parts, 1);

        Span<int> weights = parts <= 32 ? stackalloc int[parts] : new int[parts];
        weights.Fill(1);
        return Allocate(weights);
    }

    /// <summary>
    /// Splits this amount in proportion to <paramref name="weights"/>, distributing
    /// the remainder so the shares sum back to exactly this amount.
    /// </summary>
    /// <remarks>
    /// Used when a table splits a bill unevenly — by covers, or by the value of
    /// the items each guest ordered.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="weights"/> is empty, contains a negative value, or sums to zero.
    /// </exception>
    public Money[] Allocate(ReadOnlySpan<int> weights)
    {
        if (weights.IsEmpty)
        {
            throw new ArgumentException("At least one weight is required.", nameof(weights));
        }

        long totalWeight = 0;
        foreach (var weight in weights)
        {
            if (weight < 0)
            {
                throw new ArgumentException("Weights must not be negative.", nameof(weights));
            }

            totalWeight += weight;
        }

        if (totalWeight == 0)
        {
            throw new ArgumentException("Weights must not all be zero.", nameof(weights));
        }

        // Work on the magnitude so that remainder distribution behaves
        // identically for refunds and credit notes as it does for sales.
        var negative = MinorUnits < 0;
        var magnitude = negative ? -MinorUnits : MinorUnits;

        var results = new Money[weights.Length];
        var shares = new long[weights.Length];
        var distributed = 0L;

        for (var i = 0; i < weights.Length; i++)
        {
            // Int128 keeps the intermediate exact even for implausibly large totals.
            var share = (long)((Int128)magnitude * weights[i] / totalWeight);
            shares[i] = share;
            distributed += share;
        }

        // Each share is truncated by less than one minor unit, so the leftover is
        // strictly smaller than the number of non-zero weights: one pass in index
        // order is enough, and it makes the split deterministic and reproducible
        // (the same bill always splits the same way, which matters when a
        // reprinted receipt has to match the original).
        var remainder = magnitude - distributed;
        for (var i = 0; i < weights.Length && remainder > 0; i++)
        {
            if (weights[i] == 0)
            {
                continue;
            }

            shares[i]++;
            remainder--;
        }

        for (var i = 0; i < shares.Length; i++)
        {
            results[i] = new Money(negative ? -shares[i] : shares[i], Currency);
        }

        return results;
    }

    // ── Comparison and formatting ───────────────────────────────────────────

    /// <inheritdoc/>
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return MinorUnits.CompareTo(other.MinorUnits);
    }

    /// <summary>Compares two amounts of the same currency.</summary>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <summary>Compares two amounts of the same currency.</summary>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <summary>Compares two amounts of the same currency.</summary>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>Compares two amounts of the same currency.</summary>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Formats for display in the given culture, e.g. <c>12,50 €</c> in pt-PT.
    /// </summary>
    /// <remarks>
    /// Deliberately not named <c>ToString</c>. Keeping the culture-aware path under
    /// a distinct name means <see cref="ToString()"/> is unambiguously the
    /// invariant diagnostic form, and a caller who wants customer-facing output has
    /// to name the culture they mean.
    /// </remarks>
    public string Format(IFormatProvider formatProvider)
        => ToDecimal().ToString("C", formatProvider);

    /// <summary>
    /// Invariant, machine-readable form — <c>1250 EUR</c>. Used in logs and
    /// diagnostics. For anything a customer reads, pass an explicit culture to
    /// <see cref="ToString(IFormatProvider)"/> instead.
    /// </summary>
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{MinorUnits} {Currency.AlphabeticCode()}");

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine {left.Currency.AlphabeticCode()} with {right.Currency.AlphabeticCode()}.");
        }
    }
}
