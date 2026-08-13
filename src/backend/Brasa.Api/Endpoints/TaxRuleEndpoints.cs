using System.Globalization;
using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Effective-dated VAT rate endpoints (CAT-07/08) — create and list only, no
/// update/delete; a correction is a new, later-effective row, never an edit
/// to one already on file, the same "never mutate, only add" instinct
/// fiscal documents themselves follow. <b>Not yet wired into
/// <c>AddLine</c>/the fiscal document builder</b> — see <see cref="TaxRule"/>'s
/// own remarks for why that is a deliberately separate, deferred task.
/// <see cref="ResolveTaxRuleAsync"/> is the resolution logic itself,
/// verified at the API level, ready for whichever future task rewires
/// ordering to read VAT through it instead of <c>MenuItem.VatRate</c>.
/// </summary>
public static class TaxRuleEndpoints
{
    /// <summary>Maps the tax rule endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapTaxRuleEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/tax-rules", CreateTaxRuleAsync)
            .WithName("CreateTaxRule")
            .WithSummary("Adds an effective-dated VAT rate for one (alcohol band, channel, region) combination (CAT-07).")
            .Produces<TaxRuleDto>();

        group.MapGet("/tax-rules", GetTaxRulesAsync)
            .WithName("GetTaxRules")
            .WithSummary("Lists every tax rule on file.")
            .Produces<List<TaxRuleDto>>();

        group.MapGet("/tax-rules/resolve", ResolveTaxRuleAsync)
            .WithName("ResolveTaxRule")
            .WithSummary("Resolves the VAT rate in force for a combination at an instant (CAT-08). Defaults to now.")
            .Produces<ResolvedTaxRuleDto>();

        return group;
    }

    private static async Task<IResult> CreateTaxRuleAsync(
        CreateTaxRuleRequest request, CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PortugueseRegion>(request.Region, ignoreCase: true, out var region))
        {
            return Error.Validation(
                "catalog.invalid_tax_rule_region", $"\"{request.Region}\" is not a recognised Portuguese region.").ToProblem();
        }

        if (request.VatRatePercent is < 0m or > 1m)
        {
            return Error.Validation(
                "catalog.invalid_vat_rate_percent", "VAT rate must be a fraction between 0 and 1.").ToProblem();
        }

        if (!TryParseInstant(request.EffectiveFromUtc, out var effectiveFrom))
        {
            return Error.Validation(
                "catalog.invalid_tax_rule_date", $"\"{request.EffectiveFromUtc}\" is not a valid date/time.").ToProblem();
        }

        DateTimeOffset? effectiveTo = null;
        if (!string.IsNullOrWhiteSpace(request.EffectiveToUtc))
        {
            if (!TryParseInstant(request.EffectiveToUtc, out var parsedTo))
            {
                return Error.Validation(
                    "catalog.invalid_tax_rule_date", $"\"{request.EffectiveToUtc}\" is not a valid date/time.").ToProblem();
            }

            effectiveTo = parsedTo;
        }

        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            return Error.Validation(
                "catalog.invalid_tax_rule_effective_range", "Effective-to must be after effective-from.").ToProblem();
        }

        var rule = new TaxRule(
            request.IsAlcoholic, request.IsTakeaway, region, new VatRate(request.VatRatePercent), effectiveFrom, effectiveTo);

        db.TaxRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(rule.ToDto());
    }

    private static async Task<IResult> GetTaxRulesAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var rules = await db.TaxRules
            .OrderBy(r => r.IsAlcoholic).ThenBy(r => r.IsTakeaway).ThenBy(r => r.Region).ThenBy(r => r.EffectiveFromUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(rules.Select(r => r.ToDto()).ToList());
    }

    private static async Task<IResult> ResolveTaxRuleAsync(
        bool isAlcoholic,
        bool isTakeaway,
        string region,
        DateTimeOffset? atUtc,
        CatalogDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PortugueseRegion>(region, ignoreCase: true, out var parsedRegion))
        {
            return Error.Validation(
                "catalog.invalid_tax_rule_region", $"\"{region}\" is not a recognised Portuguese region.").ToProblem();
        }

        var atInstant = atUtc ?? clock.UtcNow;

        var candidates = await db.TaxRules
            .Where(r => r.IsAlcoholic == isAlcoholic && r.IsTakeaway == isTakeaway && r.Region == parsedRegion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var resolved = TaxRule.Resolve(candidates, isAlcoholic, isTakeaway, parsedRegion, atInstant);
        if (resolved is null)
        {
            return Error.NotFound(
                "catalog.tax_rule_not_found",
                $"No tax rule covers isAlcoholic={isAlcoholic}, isTakeaway={isTakeaway}, region={parsedRegion} at {atInstant:O}.")
                .ToProblem();
        }

        return Results.Ok(new ResolvedTaxRuleDto(resolved.Rate.Fraction));
    }

    private static bool TryParseInstant(string text, out DateTimeOffset value)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
}
