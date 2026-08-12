using Brasa.Modules.Catalog.Domain;

namespace Brasa.Api.Contracts;

/// <summary>
/// An effective-dated VAT rate for one (alcohol band, channel, region)
/// combination (CAT-07/08). <c>VatRatePercent</c> is a fraction, e.g.
/// <c>0.13</c> for 13% — the same convention <see cref="MenuItemDto"/>'s
/// own field of the same name already uses.
/// </summary>
public sealed record TaxRuleDto(
    Guid Id,
    bool IsAlcoholic,
    bool IsTakeaway,
    string Region,
    decimal VatRatePercent,
    string EffectiveFromUtc,
    string? EffectiveToUtc);

/// <summary>Request body to create a new effective-dated tax rule.</summary>
public sealed record CreateTaxRuleRequest(
    bool IsAlcoholic,
    bool IsTakeaway,
    string Region,
    decimal VatRatePercent,
    string EffectiveFromUtc,
    string? EffectiveToUtc);

/// <summary>The rate resolved for one combination at one instant (CAT-08), or that none was on file.</summary>
public sealed record ResolvedTaxRuleDto(decimal VatRatePercent);

/// <summary>Maps <see cref="TaxRule"/> to its wire DTO.</summary>
public static class TaxRuleDtoMappings
{
    public static TaxRuleDto ToDto(this TaxRule rule) => new(
        rule.Id,
        rule.IsAlcoholic,
        rule.IsTakeaway,
        rule.Region.ToString(),
        rule.Rate.Fraction,
        rule.EffectiveFromUtc.ToString("O"),
        rule.EffectiveToUtc?.ToString("O"));
}
