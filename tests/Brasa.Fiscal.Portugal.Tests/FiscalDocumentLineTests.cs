using Brasa.Modules.Fiscal;
using Brasa.Shared.Primitives;

namespace Brasa.Fiscal.Portugal.Tests;

/// <summary>
/// Protects a bug found live during I0's first end-to-end run: menu prices are
/// VAT-inclusive under Portuguese consumer pricing law, so a fiscal document
/// must derive net and VAT <i>from</i> the gross price. Computing it the other
/// way round — treating the menu price as net and adding VAT on top — made the
/// running order total shown to the waiter disagree with what the fiscal
/// document said the guest owed. See docs/fiscal/README.md.
/// </summary>
public sealed class FiscalDocumentLineTests
{
    [Fact]
    public void UnitPrice_is_treated_as_the_gross_VAT_inclusive_amount()
    {
        // 2x Frango na Brasa at 9.50 EUR (gross, 13% VAT) — the exact numbers
        // from the I0 reproduction.
        var line = new FiscalDocumentLine("Frango na Brasa", 2, Money.FromDecimal(9.50m), 0.13m);

        // Gross is simply price times quantity — nothing added.
        line.GrossTotal.ShouldBe(Money.FromDecimal(19.00m));

        // Net is backed OUT of the gross: 19.00 / 1.13 = 16.8141... -> 16.81.
        line.NetTotal.ShouldBe(Money.FromDecimal(16.81m));

        // VAT is whatever remains, so net + vat always reconciles to gross by
        // construction rather than by coincidence.
        line.VatAmount.ShouldBe(Money.FromDecimal(2.19m));
        (line.NetTotal + line.VatAmount).ShouldBe(line.GrossTotal);
    }

    [Fact]
    public void Alcohol_at_23_percent_reconciles_to_the_cent()
    {
        // 2x Imperial at 1.80 EUR (gross, 23% VAT — the alcohol band).
        var line = new FiscalDocumentLine("Imperial", 2, Money.FromDecimal(1.80m), 0.23m);

        line.GrossTotal.ShouldBe(Money.FromDecimal(3.60m));
        line.NetTotal.ShouldBe(Money.FromDecimal(2.93m)); // 3.60 / 1.23 = 2.9268... -> 2.93
        (line.NetTotal + line.VatAmount).ShouldBe(line.GrossTotal);
    }

    [Fact]
    public void Zero_rate_line_has_no_VAT_and_net_equals_gross()
    {
        var line = new FiscalDocumentLine("Água da Torneira", 1, Money.FromDecimal(0.50m), 0m);

        line.NetTotal.ShouldBe(line.GrossTotal);
        line.VatAmount.ShouldBe(Money.Zero);
    }

    [Theory]
    [InlineData(1, 0.13)]
    [InlineData(1, 0.23)]
    [InlineData(1, 0.06)]
    [InlineData(7, 0.13)]
    [InlineData(12, 0.23)]
    public void Net_plus_vat_always_reconciles_to_gross(int quantity, double rate)
    {
        // Exhaustive over every gross cent value in a plausible line-total
        // range, at each real Portuguese rate. This is the invariant the Z
        // report and SAF-T export both depend on.
        for (var cents = 1; cents <= 500; cents++)
        {
            var unitPrice = new Money(cents);
            var line = new FiscalDocumentLine("item", quantity, unitPrice, (decimal)rate);

            (line.NetTotal + line.VatAmount).ShouldBe(
                line.GrossTotal,
                $"quantity={quantity} rate={rate} unitPriceCents={cents}");
        }
    }
}
