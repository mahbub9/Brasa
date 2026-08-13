import ExcelJS from 'exceljs';
import { expect, test } from '@playwright/test';
import { deleteMenuItem, getMenu, importMenuItemsExcelResponse } from './support/api';
import type { ImportMenuItemsResponse } from './support/types';

// CAT-17's Excel half. POST /menu/items/import/excel shares its entire
// per-row pipeline with the CSV import (menu-import.spec.ts) once
// ExcelImportParser turns the first worksheet into the same row shape
// CsvParser produces -- these tests exist to prove that seam, not to
// re-litigate every validation case the CSV spec already covers.
//
// Builds a real .xlsx at test time via exceljs (a devDependency of this
// package only, never shipped) rather than committing a binary fixture --
// the file can never go stale and the test documents exactly what's in it.

async function buildWorkbook(rows: (string | number)[][]): Promise<Buffer> {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet('Menu');
  for (const row of rows) {
    sheet.addRow(row);
  }
  return Buffer.from(await workbook.xlsx.writeBuffer());
}

test.describe('menu item bulk import — Excel (CAT-17)', () => {
  test('imports valid rows onto the real menu and reports invalid ones per-row', async ({ request }) => {
    const menu = await getMenu(request);
    const categoryName = menu[0].name;
    const otherCategoryName = menu.length > 1 ? menu[1].name : menu[0].name;

    const suffix = Date.now().toString();
    const validName1 = `Excel Import Test A ${suffix}`;
    const validName2 = `Excel Import Test B ${suffix}`;

    const buffer = await buildWorkbook([
      ['CategoryName', 'Name', 'Description', 'Price', 'VatRate', 'IsAlcoholic'],
      [categoryName, validName1, 'Uma descrição', 4.5, 0.13, 'false'],
      [otherCategoryName, validName2, '', 3.0, 0.23, 'true'],
      ['Categoria Que Nao Existe', 'Item Invalido', '', 1.0, 0.13, 'false'],
      [categoryName, 'Item Sem Preco', '', 'not-a-number', 0.13, 'false'],
      // A wholly blank row -- Excel files routinely carry these; must be
      // skipped, not reported as yet another invalid row.
      ['', '', '', '', '', ''],
    ]);

    const response = await importMenuItemsExcelResponse(request, 'menu.xlsx', buffer);
    expect(response.status()).toBe(200);
    const body: ImportMenuItemsResponse = await response.json();

    expect(body.created).toBe(2);
    expect(body.errors).toHaveLength(2);
    expect(body.errors.find((e) => e.rowNumber === 3)?.message).toContain('Categoria Que Nao Existe');
    expect(body.errors.find((e) => e.rowNumber === 4)?.message).toContain('not-a-number');

    const updatedMenu = await getMenu(request);
    const item1 = updatedMenu.flatMap((c) => c.items).find((i) => i.name === validName1);
    const item2 = updatedMenu.flatMap((c) => c.items).find((i) => i.name === validName2);

    expect(item1, 'expected the first valid row to appear on the real menu').toBeDefined();
    expect(item1?.description).toBe('Uma descrição');
    expect(item1?.price.amount).toBeCloseTo(4.5, 2);
    expect(item1?.vatRatePercent).toBeCloseTo(0.13, 4);
    expect(item1?.isAlcoholic).toBe(false);

    expect(item2, 'expected the second valid row to appear on the real menu').toBeDefined();
    expect(item2?.isAlcoholic).toBe(true);

    if (item1) await deleteMenuItem(request, item1.id);
    if (item2) await deleteMenuItem(request, item2.id);
  });

  test('rejects an empty file, a non-.xlsx file, a corrupt .xlsx, and a header missing a required column', async ({
    request,
  }) => {
    const empty = await importMenuItemsExcelResponse(request, 'menu.xlsx', Buffer.alloc(0));
    expect(empty.status()).toBe(400);
    expect((await empty.json()).code).toBe('catalog.import_empty');

    const wrongExtension = await importMenuItemsExcelResponse(
      request,
      'menu.csv',
      await buildWorkbook([['CategoryName', 'Name', 'Price', 'VatRate']]),
    );
    expect(wrongExtension.status()).toBe(400);
    expect((await wrongExtension.json()).code).toBe('catalog.import_invalid_file');

    const corrupt = await importMenuItemsExcelResponse(request, 'menu.xlsx', Buffer.from('not actually an xlsx file'));
    expect(corrupt.status()).toBe(400);
    expect((await corrupt.json()).code).toBe('catalog.import_invalid_file');

    const missingHeader = await importMenuItemsExcelResponse(
      request,
      'menu.xlsx',
      await buildWorkbook([
        ['Name', 'Price'],
        ['Something', 1.0],
      ]),
    );
    expect(missingHeader.status()).toBe(400);
    expect((await missingHeader.json()).code).toBe('catalog.import_invalid_header');
  });

  test('the admin UI imports a real .xlsx file', async ({ page, request }) => {
    const itemName = `Excel UI Import Test ${Date.now()}`;
    const menu = await getMenu(request);
    const categoryName = menu[0].name;
    const buffer = await buildWorkbook([
      ['CategoryName', 'Name', 'Price', 'VatRate'],
      [categoryName, itemName, 2.2, 0.13],
    ]);

    await page.goto(process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174');
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    await page.getByTestId('menu-import-input').setInputFiles({
      name: 'menu.xlsx',
      mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      buffer,
    });

    await expect(page.getByTestId('menu-import-result')).toContainText('1');
    const row = page.getByTestId(`item-${itemName}`);
    await expect(row).toBeVisible();

    await page.getByTestId(`delete-item-${itemName}`).click();
    await page.getByTestId(`delete-confirm-${itemName}`).click();
  });
});
