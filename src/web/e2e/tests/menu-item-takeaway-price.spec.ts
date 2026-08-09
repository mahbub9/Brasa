import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrder,
  closeOrderAndClearTable,
  deleteMenuItem,
  getFloor,
  getMenu,
  importMenuItemsResponse,
  openOrder,
  openTakeawayOrder,
  updateMenuItemTakeawayPriceResponse,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// CAT-06 — a menu item priced differently for takeaway than dine-in (no
// table service to cover). VAT rate is unaffected; that's a separate,
// not-yet-built TaxRule concern (CAT-07/08). Delivery, the third channel
// this backlog row names, has no order path in this codebase yet at all, so
// there is nothing for a delivery price to attach to — deliberately out of
// scope here, same as CAT-17's "CSV only" caveat for Excel.
//
// Uses a freshly-imported item, isolated from the shared seeded menu other
// specs read concurrently — same reasoning as menu-price.spec.ts.

async function createTestItem(request: import('@playwright/test').APIRequestContext, price: string): Promise<{ id: string; name: string }> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const name = `Takeaway Price Test ${Date.now()}`;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},${price},0.13\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
  return { id: item.id, name };
}

test.describe('menu item takeaway price (CAT-06)', () => {
  test('sets, persists and clears a separate takeaway price', async ({ request }) => {
    const item = await createTestItem(request, '5.00');

    const withTakeawayPrice = await updateMenuItemTakeawayPriceResponse(request, item.id, 4.0);
    expect(withTakeawayPrice.status()).toBe(200);
    const body = await withTakeawayPrice.json();
    expect(body.price.amount).toBeCloseTo(5.0, 2);
    expect(body.takeawayPrice.amount).toBeCloseTo(4.0, 2);

    const menuAfterSet = await getMenu(request);
    const afterSet = menuAfterSet.flatMap((c) => c.items).find((i) => i.id === item.id);
    expect(afterSet?.takeawayPrice?.amount).toBeCloseTo(4.0, 2);

    const cleared = await updateMenuItemTakeawayPriceResponse(request, item.id, null);
    expect(cleared.status()).toBe(200);
    expect((await cleared.json()).takeawayPrice).toBeNull();

    await deleteMenuItem(request, item.id);
  });

  test('rejects a negative price and an unknown item', async ({ request }) => {
    const item = await createTestItem(request, '1.00');

    const negative = await updateMenuItemTakeawayPriceResponse(request, item.id, -1);
    expect(negative.status()).toBe(400);
    expect((await negative.json()).code).toBe('catalog.invalid_price');

    const unknown = await updateMenuItemTakeawayPriceResponse(request, crypto.randomUUID(), 1.0);
    expect(unknown.status()).toBe(404);
    expect((await unknown.json()).code).toBe('catalog.item_not_found');

    await deleteMenuItem(request, item.id);
  });

  test('a takeaway order rings up the takeaway price; a dine-in order still uses the dine-in price', async ({
    request,
  }) => {
    const item = await createTestItem(request, '5.00');
    await updateMenuItemTakeawayPriceResponse(request, item.id, 4.0);

    const takeawayOrder = await openTakeawayOrder(request, 'Takeaway Price Test');
    const withTakeawayLine = await addLine(request, takeawayOrder.id, item.id, 1);
    expect(withTakeawayLine.lines[0].lineTotal.amount).toBeCloseTo(4.0, 2);
    await closeOrder(request, takeawayOrder.id);

    const table = (await getFloor(request)).flatMap((r) => r.tables).find((t) => t.state === 'Free');
    if (!table) throw new Error('No free table for the dine-in comparison.');
    const dineInOrder = await openOrder(request, table.id, 2);
    const withDineInLine = await addLine(request, dineInOrder.id, item.id, 1);
    // Same item, dine-in order: full dine-in price, not the takeaway one.
    expect(withDineInLine.lines[0].lineTotal.amount).toBeCloseTo(5.0, 2);
    await closeOrderAndClearTable(request, dineInOrder.id, table.id);

    await deleteMenuItem(request, item.id);
  });

  test('the menu button in pos shows the takeaway price on a takeaway order, the dine-in price otherwise', async ({
    page,
    request,
  }) => {
    const item = await createTestItem(request, '5.00');
    await updateMenuItemTakeawayPriceResponse(request, item.id, 4.0);

    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);
    const itemButton = page.getByRole('button', { name: item.name });
    await expect(itemButton).toContainText('5,00 €');
    await itemButton.click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    // The table is Dirty now; clicking it again clears it back to the free pool.
    await page.getByTestId(`table-${tableLabel}`).click();

    await page.getByTestId('new-takeaway-button').click();
    await page.getByTestId('confirm-open-takeaway').click();
    await expect(itemButton).toContainText('4,00 €');
    await itemButton.click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();

    await deleteMenuItem(request, item.id);
  });

  test('the admin editor sets, edits and clears a takeaway price', async ({ page, request }) => {
    const item = await createTestItem(request, '3.00');

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    const row = page.getByTestId(`item-${item.name}`);
    await row.scrollIntoViewIfNeeded();
    await expect(row.getByTestId(`takeaway-price-add-${item.name}`)).toBeVisible();

    await row.getByTestId(`takeaway-price-add-${item.name}`).click();
    await row.getByTestId(`takeaway-price-input-${item.name}`).fill('2.50');
    await row.getByTestId(`takeaway-price-save-${item.name}`).click();
    await expect(row.getByTestId(`takeaway-price-edit-${item.name}`)).toContainText('2,50 €');

    const afterSet = await getMenu(request);
    expect(afterSet.flatMap((c) => c.items).find((i) => i.id === item.id)?.takeawayPrice?.amount).toBeCloseTo(2.5, 2);

    await row.getByTestId(`takeaway-price-clear-${item.name}`).click();
    await expect(row.getByTestId(`takeaway-price-add-${item.name}`)).toBeVisible();

    const afterClear = await getMenu(request);
    expect(afterClear.flatMap((c) => c.items).find((i) => i.id === item.id)?.takeawayPrice).toBeNull();

    await deleteMenuItem(request, item.id);
  });
});
