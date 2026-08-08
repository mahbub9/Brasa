using Brasa.Shared.Primitives;

namespace Brasa.Modules.Fiscal;

/// <summary>One priced line to appear on an issued fiscal document.</summary>
/// <param name="ItemName">Name as it should print — already a snapshot from the order line.</param>
/// <param name="Quantity">How many.</param>
/// <param name="UnitPrice">
/// Unit price as charged — <b>VAT-inclusive</b>. Portuguese (and EU) consumer
/// pricing law requires the price shown to a guest to be the final amount they
/// pay, so this is what appears on the menu and on the running order total, not
/// a net price with VAT added afterwards.
/// </param>
/// <param name="VatRateFraction">VAT rate as a fraction, e.g. <c>0.13m</c> for 13%.</param>
/// <remarks>
/// Net and VAT are <b>derived</b> from the gross price, not the other way
/// round. Getting this backwards makes the amount shown to the waiter during
/// service disagree with the amount the fiscal document says the guest owes —
/// which is exactly the kind of discrepancy the Z report must never have. See
/// <c>docs/fiscal/README.md</c>.
/// </remarks>
public sealed record FiscalDocumentLine(string ItemName, int Quantity, Money UnitPrice, decimal VatRateFraction)
{
    /// <summary>Gross line total — what the guest pays. <see cref="UnitPrice"/> already includes VAT.</summary>
    public Money GrossTotal => UnitPrice * Quantity;

    /// <summary>
    /// Net (pre-VAT) line total, backed out of the VAT-inclusive gross total:
    /// <c>net = gross / (1 + rate)</c>, rounded to the nearest cent.
    /// </summary>
    /// <remarks>
    /// Computed directly in decimal via <see cref="Money.ToDecimal"/> /
    /// <see cref="Money.FromDecimal(decimal, Shared.Primitives.CurrencyCode)"/>
    /// rather than through <see cref="Money"/>'s multiplication operator: this is
    /// a division, which <c>Money</c> deliberately does not expose as an
    /// operator — see <c>docs/architecture/money.md</c> on why splitting uses
    /// <c>Allocate</c> rather than a general division API.
    /// </remarks>
    public Money NetTotal
    {
        get
        {
            var netAmount = Math.Round(GrossTotal.ToDecimal() / (1 + VatRateFraction), 2, MidpointRounding.AwayFromZero);
            return Money.FromDecimal(netAmount, GrossTotal.Currency);
        }
    }

    /// <summary>VAT contained within the gross total for this line.</summary>
    public Money VatAmount => GrossTotal - NetTotal;
}
