namespace Brasa.Shared.Primitives;

/// <summary>
/// ISO 4217 currencies this system can represent.
/// </summary>
/// <remarks>
/// <para>
/// Portugal is euro-only, so in practice every amount in the system today is
/// <see cref="Eur"/>. The type exists so that expanding to a non-euro market
/// later is an additive change rather than a migration of every money column.
/// </para>
/// <para>
/// <see cref="Eur"/> is deliberately <c>0</c> so that <c>default(Money)</c> is
/// "zero euros" rather than an invalid amount in an unknown currency.
/// </para>
/// </remarks>
public enum CurrencyCode
{
    /// <summary>Euro. The default, and the only currency used in Portugal.</summary>
    Eur = 0,
}

/// <summary>Metadata about a <see cref="CurrencyCode"/>.</summary>
public static class CurrencyCodeExtensions
{
    /// <summary>
    /// Number of decimal places the currency subdivides into — the exponent that
    /// converts minor units to major units. EUR has 2 (100 cents to the euro).
    /// </summary>
    public static int DecimalPlaces(this CurrencyCode currency) => currency switch
    {
        CurrencyCode.Eur => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, "Unknown currency."),
    };

    /// <summary>Number of minor units in one major unit — 100 for EUR.</summary>
    public static long MinorUnitsPerMajor(this CurrencyCode currency) => currency switch
    {
        CurrencyCode.Eur => 100L,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, "Unknown currency."),
    };

    /// <summary>The three-letter ISO 4217 alphabetic code, e.g. <c>EUR</c>.</summary>
    public static string AlphabeticCode(this CurrencyCode currency) => currency switch
    {
        CurrencyCode.Eur => "EUR",
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, "Unknown currency."),
    };
}
