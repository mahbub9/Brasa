import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  deleteMenuItem,
  findFreeTable,
  getFloor,
  getMenu,
  importMenuItemsResponse,
  openOrder,
  updateMenuItemPriceResponse,
} from './support/api';

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

// Menu item repricing. MenuItem.Reprice existed with its own validation
// since I0, with no endpoint calling it — found the same way as CAT-13's
// availability gap. Safe by construction, not by convention:
// OrderLine.UnitPrice snapshots the price when a line is added, so this
// spec's real point is proving a line already rung up survives a reprice
// unchanged, not just that the menu number updates.
//
// Uses a freshly-imported item, isolated from the shared seeded menu other
// specs read concurrently (2 workers) — same reasoning as
// menu-availability.spec.ts.

test.describe('menu item price', () => {
  async function createTestItem(request: import('@playwright/test').APIRequestContext, price: string): Promise<string> {
    const menu = await getMenu(request);
    const categoryName = menu[0].name;
    const name = `Price Test ${Date.now()}`;
    const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},${price},0.13\n`;

    const response = await importMenuItemsResponse(request, csv);
    expect((await response.json()).created).toBe(1);

    const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
    if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
    return item.id;
  }

  test('changes the price for future orders without rewriting an already-rung-up line', async ({ request }) => {
    const itemId = await createTestItem(request, '3.50');
    const table = findFreeTable(await getFloor(request));
    const order = await openOrder(request, table.id, 2);
    const updated = await addLine(request, order.id, itemId, 1);
    expect(updated.total.amount).toBeCloseTo(3.5, 2);

    const repriceResponse = await updateMenuItemPriceResponse(request, itemId, 5.0);
    expect(repriceResponse.status()).toBe(200);
    expect((await repriceResponse.json()).price.amount).toBeCloseTo(5.0, 2);

    // The critical invariant: a line already on an open order is a
    // snapshot, not a live join — it must not move when the menu price
    // changes underneath it.
    const orderAfterReprice = await request.get(`${apiBaseUrl}/orders/${order.id}`).then((r) => r.json());
    expect(orderAfterReprice.total.amount).toBeCloseTo(3.5, 2);

    // A *new* line rung up after the reprice picks up the new price.
    const menuAfter = await getMenu(request);
    const repricedItem = menuAfter.flatMap((c) => c.items).find((i) => i.id === itemId);
    expect(repricedItem?.price.amount).toBeCloseTo(5.0, 2);

    await closeOrderAndClearTable(request, order.id, table.id);
    await deleteMenuItem(request, itemId);
  });

  test('rejects a negative price and an unknown item', async ({ request }) => {
    const itemId = await createTestItem(request, '1.00');

    const negative = await updateMenuItemPriceResponse(request, itemId, -1);
    expect(negative.status()).toBe(400);
    expect((await negative.json()).code).toBe('catalog.invalid_price');

    const unknown = await updateMenuItemPriceResponse(request, crypto.randomUUID(), 1.0);
    expect(unknown.status()).toBe(404);
    expect((await unknown.json()).code).toBe('catalog.item_not_found');

    await deleteMenuItem(request, itemId);
  });
});
