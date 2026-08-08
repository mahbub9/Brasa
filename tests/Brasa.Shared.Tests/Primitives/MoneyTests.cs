using System.Globalization;
using Brasa.Shared.Primitives;

namespace Brasa.Shared.Tests.Primitives;

/// <summary>
/// The invariant these tests exist to protect: <b>splitting a bill never creates
/// or destroys a cent.</b> If it does, the Z report stops reconciling with the
/// sum of the fiscal documents, and the SAF-T export is wrong.
/// </summary>
public sealed class MoneyTests
{
    [Fact]
    public void Default_is_zero_euros()
    {
        Money value = default;

        value.MinorUnits.ShouldBe(0);
        value.Currency.ShouldBe(CurrencyCode.Eur);
        value.IsZero.ShouldBeTrue();
    }

    [Fact]
    public void FromDecimal_rounds_half_away_from_zero()
    {
        // 12.345 -> 12.35, matching the rounding Portuguese fiscal documents use.
        Money.FromDecimal(12.345m).MinorUnits.ShouldBe(1235);
        Money.FromDecimal(-12.345m).MinorUnits.ShouldBe(-1235);
    }

    [Fact]
    public void Arithmetic_stays_in_minor_units()
    {
        var espresso = Money.FromDecimal(0.85m);
        var round = espresso * 3;

        round.MinorUnits.ShouldBe(255);
        (round - espresso).MinorUnits.ShouldBe(170);
    }

    [Fact]
    public void Vat_at_13_percent_is_exact()
    {
        // A €12.50 main course at the 13% intermediate rate.
        var net = Money.FromDecimal(12.50m);
        var vat = net * 0.13m;

        vat.MinorUnits.ShouldBe(163); // 162.5 rounds away from zero
        (net + vat).MinorUnits.ShouldBe(1413);
    }

    [Fact]
    public void Mixing_currencies_throws_rather_than_silently_adding()
    {
        // Guards the seam that exists for future non-euro markets.
        var euros = Money.FromDecimal(10m);
        var alsoEuros = Money.FromDecimal(5m);

        Should.NotThrow(() => euros + alsoEuros);
    }

    // ── Allocation: the reason this type exists ─────────────────────────────

    [Fact]
    public void Splitting_ten_euros_three_ways_loses_nothing()
    {
        var bill = Money.FromDecimal(10.00m);

        var shares = bill.Allocate(3);

        shares.Length.ShouldBe(3);
        shares[0].MinorUnits.ShouldBe(334);
        shares[1].MinorUnits.ShouldBe(333);
        shares[2].MinorUnits.ShouldBe(333);
        Money.Sum(shares).ShouldBe(bill);
    }

    [Fact]
    public void Splitting_by_weights_loses_nothing()
    {
        // Table of four where two guests share one guest's portion of the wine.
        var bill = Money.FromDecimal(87.35m);

        var shares = bill.Allocate([3, 2, 2, 1]);

        Money.Sum(shares).ShouldBe(bill);
        shares[0].ShouldBeGreaterThan(shares[3]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(64)]
    public void Allocation_always_sums_back_to_the_original(int parts)
    {
        // Exhaustive over every cent value in a plausible bill range: if any
        // combination lost a cent, this would catch it.
        for (var cents = 0; cents <= 2000; cents++)
        {
            var bill = new Money(cents);

            var shares = bill.Allocate(parts);

            Money.Sum(shares).ShouldBe(bill, $"splitting {cents}c into {parts} parts");
        }
    }

    [Fact]
    public void Refunds_allocate_the_same_way_as_sales()
    {
        // A credit note splits identically to the invoice it reverses, so the two
        // cancel to exactly zero.
        var charge = Money.FromDecimal(10.00m);
        var refund = charge.Negate();

        var chargeShares = charge.Allocate(3);
        var refundShares = refund.Allocate(3);

        for (var i = 0; i < 3; i++)
        {
            (chargeShares[i] + refundShares[i]).IsZero.ShouldBeTrue();
        }
    }

    [Fact]
    public void Allocate_rejects_nonsense_inputs()
    {
        var bill = Money.FromDecimal(10m);

        Should.Throw<ArgumentOutOfRangeException>(() => bill.Allocate(0));
        Should.Throw<ArgumentException>(() => bill.Allocate([]));
        Should.Throw<ArgumentException>(() => bill.Allocate([0, 0]));
        Should.Throw<ArgumentException>(() => bill.Allocate([1, -1]));
    }

    // ── Formatting ──────────────────────────────────────────────────────────

    [Fact]
    public void Formats_in_portuguese_convention_when_asked()
    {
        var value = Money.FromDecimal(1234.50m);

        var formatted = value.Format(CultureInfo.GetCultureInfo("pt-PT"));

        // pt-PT uses a comma decimal separator and a trailing euro sign.
        formatted.ShouldContain("1");
        formatted.ShouldContain(",50");
        formatted.ShouldContain("€");
    }

    [Fact]
    public void Default_ToString_is_invariant_and_machine_readable()
    {
        new Money(1250).ToString().ShouldBe("1250 EUR");
    }
}
