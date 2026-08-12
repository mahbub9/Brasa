import { expect, test } from '@playwright/test';
import {
  closeOrderAndClearTable,
  deleteMenuItem,
  getMenu,
  importMenuItemsResponse,
  updateMenuItemCouvert,
  updateMenuItemCouvertResponse,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// CAT-12 — couvert (bread, olives and the like) is a plain menu-item tag,
// not a filter: marking an item couvert never removes it from GET /menu
// (unlike CAT-11's schedule), it only adds pos's dedicated one-tap "add at
// cover count" affordance alongside the item's normal spot on the menu
// grid. AddLine itself needs no changes — adding couvert is the same call
// as any other line, which is what already makes "charged only when
// consumed" true for every item today.
//
// Uses a freshly-imported item, isolated from the shared seeded menu other
// specs read concurrently — same reasoning as menu-item-takeaway-price.spec.ts.

async function createTestItem(request: import('@playwright/test').APIRequestContext): Promise<{ id: string; name: string }> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const name = `Couvert Test ${Date.now()}`;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},1.50,0.13\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
  return { id: item.id, name };
}

test.describe('menu item couvert (CAT-12)', () => {
  test('sets and clears the couvert flag without ever removing the item from GET /menu', async ({ request }) => {
    const item = await createTestItem(request);

    const initial = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.id === item.id);
    expect(initial?.isCouvert).toBe(false);

    const marked = await updateMenuItemCouvert(request, item.id, true);
    expect(marked.isCouvert).toBe(true);

    const afterMark = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.id === item.id);
    expect(afterMark).toBeDefined();
    expect(afterMark?.isCouvert).toBe(true);

    const unmarked = await updateMenuItemCouvert(request, item.id, false);
    expect(unmarked.isCouvert).toBe(false);

    await deleteMenuItem(request, item.id);
  });

  test('rejects an unknown item', async ({ request }) => {
    const unknown = await updateMenuItemCouvertResponse(request, crypto.randomUUID(), true);
    expect(unknown.status()).toBe(404);
    expect((await unknown.json()).code).toBe('catalog.item_not_found');
  });

  test('the couvert bar in pos rings the item up at the order\'s cover count; a takeaway order never shows it', async ({
    page,
    request,
  }) => {
    const item = await createTestItem(request);
    await updateMenuItemCouvert(request, item.id, true);
    const itemButton = page.getByRole('button', { name: item.name });

    // Takeaway first — no cover count, no couvert bar. The item is still on
    // the regular menu grid (couvert never filters GET /menu), so a normal
    // tap there both proves the bar's absence and gives the order a line
    // to close with.
    await page.goto('/');
    await page.getByTestId('new-takeaway-button').click();
    await page.getByTestId('confirm-open-takeaway').click();
    await expect(page.getByTestId('couvert-bar')).toHaveCount(0);
    await itemButton.click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();

    // Dine-in: the bar appears, and one tap rings the item up at the
    // order's own cover count (3), not the usual quantity of 1.
    const tableLabel = await openAnyFreeTable(page, 3);
    const couvertBar = page.getByTestId('couvert-bar');
    await expect(couvertBar).toBeVisible();
    await couvertBar.getByTestId(`couvert-add-${item.name}`).click();

    const line = page.locator('.order-lines li', { hasText: item.name });
    await expect(line.locator('.order-line-qty')).toHaveText('3×');

    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    // The table is Dirty now; clicking it again clears it back to the free pool.
    await page.getByTestId(`table-${tableLabel}`).click();

    await deleteMenuItem(request, item.id);
  });

  test('the admin editor marks and unmarks an item as couvert', async ({ page, request }) => {
    const item = await createTestItem(request);

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    const row = page.getByTestId(`item-${item.name}`);
    await row.scrollIntoViewIfNeeded();
    await expect(row.getByTestId(`toggle-couvert-${item.name}`)).toBeVisible();

    await row.getByTestId(`toggle-couvert-${item.name}`).click();
    await expect(row.getByText('Couvert', { exact: true })).toBeVisible();

    const afterMark = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.id === item.id);
    expect(afterMark?.isCouvert).toBe(true);

    await row.getByTestId(`toggle-couvert-${item.name}`).click();
    await expect(row.getByText('Couvert', { exact: true })).toHaveCount(0);

    const afterUnmark = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.id === item.id);
    expect(afterUnmark?.isCouvert).toBe(false);

    await deleteMenuItem(request, item.id);
  });
});
