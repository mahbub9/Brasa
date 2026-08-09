import { expect, test } from '@playwright/test';
import {
  deleteMenuItem,
  getMenu,
  getMenuAll,
  importMenuItemsResponse,
  updateMenuCategoryVisibilityResponse,
  updateMenuItemAvailabilityResponse,
} from './support/api';

// WEB-10's menu editor slice. GET /menu is guest-facing and filters to what
// a guest may actually order — GET /menu/all exists because a management
// screen needs to *see* a hidden category or an 86'd item to turn it back
// on, which the filtered endpoint can never show it. See
// CatalogEndpoints.GetMenuAllAsync's own remarks.

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

test.describe('GET /menu/all', () => {
  test('includes hidden categories and unavailable items that GET /menu excludes', async ({ request }) => {
    const csv = `CategoryName,Name,Price,VatRate\nEntradas,Admin Menu Test ${Date.now()},2.00,0.13\n`;
    const importResponse = await importMenuItemsResponse(request, csv);
    expect((await importResponse.json()).created).toBe(1);

    const all = await getMenuAll(request);
    const entradas = all.find((c) => c.name === 'Entradas');
    if (!entradas) throw new Error('Seeded "Entradas" category not found.');
    const item = entradas.items.find((i) => i.name.startsWith('Admin Menu Test'));
    if (!item) throw new Error('Freshly-imported test item not found in GET /menu/all.');
    expect(item.isAvailable).toBe(true);

    const disableResponse = await updateMenuItemAvailabilityResponse(request, item.id, false);
    expect(disableResponse.status()).toBe(200);

    // Gone from the guest-facing menu...
    const guestMenu = await getMenu(request);
    const guestEntradas = guestMenu.find((c) => c.name === 'Entradas');
    expect(guestEntradas?.items.some((i) => i.id === item.id)).toBe(false);

    // ...but still visible to management, correctly flagged unavailable.
    const allAfter = await getMenuAll(request);
    const managedItem = allAfter.find((c) => c.name === 'Entradas')?.items.find((i) => i.id === item.id);
    expect(managedItem?.isAvailable).toBe(false);

    await deleteMenuItem(request, item.id);
  });

  test('includes a hidden category and reflects isVisible correctly', async ({ request }) => {
    const hideResponse = await updateMenuCategoryVisibilityResponse(
      request,
      (await getMenuAll(request)).find((c) => c.name === 'Sobremesas')!.id,
      false,
    );
    expect(hideResponse.status()).toBe(200);

    const all = await getMenuAll(request);
    const sobremesas = all.find((c) => c.name === 'Sobremesas');
    expect(sobremesas).toBeDefined();
    expect(sobremesas?.isVisible).toBe(false);
    expect(sobremesas?.items.length).toBeGreaterThan(0);

    // Restore — other specs (e.g. menu-category-visibility.spec.ts) assume
    // Sobremesas starts visible.
    await updateMenuCategoryVisibilityResponse(request, sobremesas!.id, true);
  });
});

test.describe('admin menu editor (WEB-10)', () => {
  test('toggling a category\'s visibility from the UI is reflected immediately', async ({ page }) => {
    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    const category = page.getByTestId('category-Sobremesas');
    await category.scrollIntoViewIfNeeded();
    await expect(category).toContainText('Visível');

    await page.getByTestId('toggle-category-Sobremesas').click();
    await expect(category).toContainText('Oculta');

    // Restore for other specs.
    await page.getByTestId('toggle-category-Sobremesas').click();
    await expect(category).toContainText('Visível');
  });

  test('86-ing and repricing an item from the UI persists to the API', async ({ page, request }) => {
    const itemName = `Admin UI Test ${Date.now()}`;
    const csv = `CategoryName,Name,Price,VatRate\nEntradas,${itemName},4.00,0.13\n`;
    const importResponse = await importMenuItemsResponse(request, csv);
    expect((await importResponse.json()).created).toBe(1);

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    const row = page.getByTestId(`item-${itemName}`);
    await row.scrollIntoViewIfNeeded();
    await expect(row).toContainText('Disponível');

    await page.getByTestId(`toggle-availability-${itemName}`).click();
    await expect(row).toContainText('Indisponível');

    await page.getByTestId(`price-edit-${itemName}`).click();
    await page.getByTestId(`price-input-${itemName}`).fill('6.50');
    await page.getByTestId(`price-save-${itemName}`).click();
    await expect(row).toContainText('6,50 €');

    const managed = await getMenuAll(request);
    const item = managed.flatMap((c) => c.items).find((i) => i.name === itemName);
    expect(item?.isAvailable).toBe(false);
    expect(item?.price.amount).toBeCloseTo(6.5, 2);

    await page.getByTestId(`delete-item-${itemName}`).click();
    await page.getByTestId(`delete-confirm-${itemName}`).click();
    await expect(row).toHaveCount(0);
  });

  test('importing a CSV creates items visible on the menu screen without a reload', async ({ page }) => {
    const itemName = `Admin Import Test ${Date.now()}`;
    const csv = `CategoryName,Name,Price,VatRate\nEntradas,${itemName},1.10,0.13\n`;

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    await page.getByTestId('menu-import-input').setInputFiles({
      name: 'import.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv, 'utf-8'),
    });

    await expect(page.getByTestId('menu-import-result')).toContainText('1');
    const row = page.getByTestId(`item-${itemName}`);
    await expect(row).toBeVisible();

    // Cleanup.
    await page.getByTestId(`delete-item-${itemName}`).click();
    await page.getByTestId(`delete-confirm-${itemName}`).click();
  });
});
