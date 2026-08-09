import { expect, test } from '@playwright/test';
import { deleteMenuItem, getMenu, importMenuItemsResponse } from './support/api';
import type { ImportMenuItemsResponse } from './support/types';

// CAT-17 — bulk CSV import for menu items. Ships ahead of any admin UI to
// upload a file from, the same way CAT-02/CAT-18 shipped ahead of theirs.
// Rows import independently: one bad row is reported, not fatal to the rest.

test.describe('menu item bulk import', () => {
  test('imports valid rows onto the real menu and reports invalid ones per-row', async ({ request }) => {
    const menu = await getMenu(request);
    const categoryName = menu[0].name;
    const otherCategoryName = menu.length > 1 ? menu[1].name : menu[0].name;

    // Unique names so this run's rows can't collide with a previous run's
    // leftovers in the (never-reset, QA-02) dev database.
    const suffix = Date.now().toString();
    const validName1 = `Import Test A ${suffix}`;
    const validName2 = `Import Test B ${suffix}`;

    const csv = [
      'CategoryName,Name,Description,Price,VatRate,IsAlcoholic',
      `${categoryName},${validName1},Uma descrição,4.50,0.13,false`,
      `${otherCategoryName},${validName2},,3.00,0.23,true`,
      `Categoria Que Nao Existe,Item Invalido,,1.00,0.13,false`,
      `${categoryName},Item Sem Preco,,not-a-number,0.13,false`,
    ].join('\n');

    const response = await importMenuItemsResponse(request, csv);
    expect(response.status()).toBe(200);
    const body: ImportMenuItemsResponse = await response.json();

    expect(body.created).toBe(2);
    expect(body.errors).toHaveLength(2);
    expect(body.errors.find((e) => e.rowNumber === 3)?.message).toContain('Categoria Que Nao Existe');
    expect(body.errors.find((e) => e.rowNumber === 4)?.message).toContain('not-a-number');

    // The two valid rows are real menu items now, not just an import receipt.
    const updatedMenu = await getMenu(request);
    const item1 = updatedMenu.flatMap((c) => c.items).find((i) => i.name === validName1);
    const item2 = updatedMenu.flatMap((c) => c.items).find((i) => i.name === validName2);

    expect(item1, 'expected the first valid row to appear on the real menu').toBeDefined();
    expect(item1?.description).toBe('Uma descrição');
    expect(item1?.price.amount).toBeCloseTo(4.5, 2);
    expect(item1?.vatRatePercent).toBeCloseTo(0.13, 4);
    expect(item1?.isAlcoholic).toBe(false);

    expect(item2, 'expected the second valid row to appear on the real menu').toBeDefined();
    expect(item2?.description).toBeNull();
    expect(item2?.price.amount).toBeCloseTo(3.0, 2);
    expect(item2?.isAlcoholic).toBe(true);

    // Cleanup: give the seeded menu back the way every other spec does.
    if (item1) await deleteMenuItem(request, item1.id);
    if (item2) await deleteMenuItem(request, item2.id);
  });

  test('rejects a CSV with no rows at all', async ({ request }) => {
    const response = await importMenuItemsResponse(request, '');
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('catalog.import_empty');
  });

  test('rejects a CSV whose header is missing a required column', async ({ request }) => {
    const response = await importMenuItemsResponse(request, 'Name,Price\nSomething,1.00\n');
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('catalog.import_invalid_header');
  });
});
