import { expect, test } from '@playwright/test';
import {
  deleteMenuItem,
  getMenu,
  getMenuAll,
  importMenuItemsResponse,
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

  test('every category\'s isVisible matches whether it actually appears in GET /menu', async ({ request }) => {
    // Deliberately read-only — menu-category-visibility.spec.ts (CAT-01)
    // already proves hide/show correctness by actually mutating Sobremesas,
    // and now legitimately races with this suite's own admin UI test doing
    // the same (see that spec's file comment). A snapshot consistency
    // check needs no mutation of its own and holds regardless of what any
    // concurrent spec is doing to any category at the moment it runs.
    const [all, guestMenu] = await Promise.all([getMenuAll(request), getMenu(request)]);
    const guestCategoryIds = new Set(guestMenu.map((c) => c.id));

    for (const category of all) {
      expect(category.isVisible).toBe(guestCategoryIds.has(category.id));
    }
  });
});

test.describe('admin menu editor (WEB-10)', () => {
  test('toggling a category\'s visibility from the UI is reflected immediately', async ({ page }) => {
    // Sobremesas is also touched by menu-category-visibility.spec.ts under
    // real parallel workers (see that file's comment on why it's the only
    // safe shared category left) — this doesn't assume a specific starting
    // state, only that clicking the toggle flips whatever it currently
    // shows, retrying if a concurrent sibling's own toggle lands first.
    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    const category = page.getByTestId('category-Sobremesas');
    await category.scrollIntoViewIfNeeded();

    let toggled = false;
    for (let attempt = 1; attempt <= 5 && !toggled; attempt += 1) {
      const wasVisible = (await category.innerText()).includes('Visível');
      await page.getByTestId('toggle-category-Sobremesas').click();
      try {
        await expect(category).toContainText(wasVisible ? 'Oculta' : 'Visível', { timeout: 3_000 });
        toggled = true;
      } catch {
        // Fall through and retry with a freshly-read starting state.
      }
    }

    expect(toggled).toBe(true);

    // Best-effort restore — not asserted; menu-category-visibility.spec.ts
    // no longer assumes Sobremesas starts visible either.
    await page.getByTestId('toggle-category-Sobremesas').click().catch(() => {});
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
