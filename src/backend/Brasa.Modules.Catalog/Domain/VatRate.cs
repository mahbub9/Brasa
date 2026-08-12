namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// A VAT percentage, as a fraction (0.13 for 13%).
/// </summary>
/// <remarks>
/// <para>
/// This is still the value <c>MenuItem.VatRate</c> carries live. The full
/// effective-dated model, <see cref="TaxRule"/> (CAT-07/08), exists now —
/// keyed by alcohol band × channel × region — but nothing in
/// <c>AddLine</c>/<c>AddComboLineAsync</c>/the fiscal document builder
/// resolves through it yet; see <see cref="TaxRule"/>'s own remarks for
/// why that rewiring is a deliberately separate task.
/// </para>
/// <para>
/// It is still never a bare <c>decimal</c> scattered through the codebase: one
/// named type, one place that validates the range, one place a future
/// effective-dated lookup replaces. See <c>docs/fiscal/README.md</c> — rates are
/// data, not constants, and are unconfirmed by an accountant.
/// </para>
/// </remarks>
public readonly record struct VatRate
{
    /// <summary>Creates a rate from a fraction, e.g. <c>0.13m</c> for 13%.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The fraction is outside 0–1.</exception>
    public VatRate(decimal fraction)
    {
        if (fraction is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "VAT rate must be between 0 and 1.");
        }

        Fraction = fraction;
    }

    /// <summary>The rate as a fraction of the net price.</summary>
    public decimal Fraction { get; }

    /// <summary>13% — meals and non-alcoholic drinks, mainland, as of 2026. Unconfirmed by an accountant.</summary>
    public static VatRate IntermediateMainland => new(0.13m);

    /// <summary>23% — alcoholic drinks, mainland, as of 2026. Unconfirmed by an accountant.</summary>
    public static VatRate StandardMainland => new(0.23m);

    /// <summary>6% — reduced rate, mainland, as of 2026. Unconfirmed by an accountant.</summary>
    public static VatRate ReducedMainland => new(0.06m);

    /// <inheritdoc/>
    public override string ToString() => Fraction.ToString("P0", System.Globalization.CultureInfo.InvariantCulture);
}
