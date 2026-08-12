import { expect, test } from '@playwright/test';
import { createTaxRule, createTaxRuleResponse, resolveTaxRule, resolveTaxRuleResponse } from './support/api';

// Tax rules (CAT-07/08) — effective-dated VAT rates keyed by alcohol band x
// channel x region, replacing the hardcoded-constant instinct with real
// data. Not yet wired into AddLine or the fiscal document builder, which
// still read MenuItem.VatRate directly — see TaxRule's own remarks for why
// that rewiring is a deliberately separate, more fiscally-sensitive task.
// API-only, same "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/
// FLR-05 already established.

test.describe('tax rules (CAT-07/08)', () => {
  test('resolves the seeded mainland rates for both bands and both channels', async ({ request }) => {
    const nonAlcoholicDineIn = await resolveTaxRule(
      request, 'isAlcoholic=false&isTakeaway=false&region=Continental',
    );
    expect(nonAlcoholicDineIn.vatRatePercent).toBe(0.13);

    const alcoholicDineIn = await resolveTaxRule(
      request, 'isAlcoholic=true&isTakeaway=false&region=Continental',
    );
    expect(alcoholicDineIn.vatRatePercent).toBe(0.23);

    const nonAlcoholicTakeaway = await resolveTaxRule(
      request, 'isAlcoholic=false&isTakeaway=true&region=Continental',
    );
    expect(nonAlcoholicTakeaway.vatRatePercent).toBe(0.13);

    const alcoholicTakeaway = await resolveTaxRule(
      request, 'isAlcoholic=true&isTakeaway=true&region=Continental',
    );
    expect(alcoholicTakeaway.vatRatePercent).toBe(0.23);
  });

  test('a superseding rule wins within its own window; nothing resolves before the earliest rule starts', async ({
    request,
  }) => {
    // Madeira, non-alcoholic dine-in — untouched by the seeded Continental
    // rows or any other spec in this file, so this test owns the whole
    // timeline for this combination.
    const older = await createTaxRule(request, {
      isAlcoholic: false,
      isTakeaway: false,
      region: 'Madeira',
      vatRatePercent: 0.06,
      effectiveFromUtc: '2020-01-01T00:00:00Z',
      effectiveToUtc: '2021-01-01T00:00:00Z',
    });
    expect(older.vatRatePercent).toBe(0.06);

    const newer = await createTaxRule(request, {
      isAlcoholic: false,
      isTakeaway: false,
      region: 'Madeira',
      vatRatePercent: 0.13,
      effectiveFromUtc: '2021-01-01T00:00:00Z',
    });
    expect(newer.vatRatePercent).toBe(0.13);
    expect(newer.effectiveToUtc).toBeNull();

    const duringOlder = await resolveTaxRule(
      request, 'isAlcoholic=false&isTakeaway=false&region=Madeira&atUtc=2020-06-01T00:00:00Z',
    );
    expect(duringOlder.vatRatePercent).toBe(0.06);

    const afterSupersession = await resolveTaxRule(
      request, 'isAlcoholic=false&isTakeaway=false&region=Madeira&atUtc=2022-01-01T00:00:00Z',
    );
    expect(afterSupersession.vatRatePercent).toBe(0.13);

    const beforeEitherRule = await resolveTaxRuleResponse(
      request, 'isAlcoholic=false&isTakeaway=false&region=Madeira&atUtc=2019-01-01T00:00:00Z',
    );
    expect(beforeEitherRule.status()).toBe(404);
    expect((await beforeEitherRule.json()).code).toBe('catalog.tax_rule_not_found');
  });

  test('rejects an unrecognised region, an out-of-range percentage, an unparsable date and a backwards range', async ({
    request,
  }) => {
    const valid = {
      isAlcoholic: false,
      isTakeaway: false,
      region: 'Azores',
      vatRatePercent: 0.13,
      effectiveFromUtc: '2024-01-01T00:00:00Z',
    };

    const badRegion = await createTaxRuleResponse(request, { ...valid, region: 'Lisbon' });
    expect(badRegion.status()).toBe(400);
    expect((await badRegion.json()).code).toBe('catalog.invalid_tax_rule_region');

    const badPercent = await createTaxRuleResponse(request, { ...valid, vatRatePercent: 1.5 });
    expect(badPercent.status()).toBe(400);
    expect((await badPercent.json()).code).toBe('catalog.invalid_vat_rate_percent');

    const badDate = await createTaxRuleResponse(request, { ...valid, effectiveFromUtc: 'not-a-date' });
    expect(badDate.status()).toBe(400);
    expect((await badDate.json()).code).toBe('catalog.invalid_tax_rule_date');

    const backwardsRange = await createTaxRuleResponse(request, {
      ...valid,
      effectiveFromUtc: '2024-06-01T00:00:00Z',
      effectiveToUtc: '2024-01-01T00:00:00Z',
    });
    expect(backwardsRange.status()).toBe(400);
    expect((await backwardsRange.json()).code).toBe('catalog.invalid_tax_rule_effective_range');

    const resolveBadRegion = await resolveTaxRuleResponse(request, 'isAlcoholic=false&isTakeaway=false&region=Lisbon');
    expect(resolveBadRegion.status()).toBe(400);
    expect((await resolveBadRegion.json()).code).toBe('catalog.invalid_tax_rule_region');
  });

  test('404s resolving a combination with no rule on file', async ({ request }) => {
    // Azores, alcoholic takeaway — nothing in this file ever creates a rule
    // for this exact combination.
    const response = await resolveTaxRuleResponse(request, 'isAlcoholic=true&isTakeaway=true&region=Azores');
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('catalog.tax_rule_not_found');
  });
});
