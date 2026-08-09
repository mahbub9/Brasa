import { expect, test } from '@playwright/test';
import {
  closeOrderAndClearTable,
  deleteMenuItem,
  findFreeTable,
  findMenuItem,
  getFloor,
  getMenu,
  importMenuItemsResponse,
  updateMenuItemAvailabilityResponse,
} from './support/api';

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

// CAT-13 — 86'ing a menu item. MarkAvailable/MarkUnavailable have existed on
// the domain since I0 and AddLine already rejected an unavailable item with
// catalog.item_unavailable, but no endpoint ever called either one, so
// IsAvailable could never actually become false — this is the endpoint that
// makes the guard reachable. Ships ahead of any UI that will call it, the
// same way CAT-02/CAT-17/CAT-18 shipped ahead of theirs.
//
// Uses a freshly-imported item rather than a shared seeded one — toggling a
// seeded item's availability while other specs run concurrently against the
// same dev database (2 workers) could make an unrelated spec's order fail
// to ring up for a reason that has nothing to do with what it's testing.

test.describe('menu item availability (86-ing)', () => {
  async function createTestItem(request: import('@playwright/test').APIRequestContext): Promise<string> {
    const menu = await getMenu(request);
    const categoryName = menu[0].name;
    const name = `Availability Test ${Date.now()}`;
    const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},2.00,0.13\n`;

    const response = await importMenuItemsResponse(request, csv);
    const body = await response.json();
    expect(body.created).toBe(1);

    const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
    if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
    return item.id;
  }

  test('86-ing hides an item from the menu and blocks ordering it; un-86-ing restores both', async ({ request }) => {
    const itemId = await createTestItem(request);

    const eightySix = await updateMenuItemAvailabilityResponse(request, itemId, false);
    expect(eightySix.status()).toBe(200);
    expect((await eightySix.json()).isAvailable).toBe(false);

    const menuWhile86d = await getMenu(request);
    expect(menuWhile86d.flatMap((c) => c.items).some((i) => i.id === itemId)).toBe(false);

    const table = findFreeTable(await getFloor(request));
    const orderResponse = await request.post(`${apiBaseUrl}/orders`, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      data: { tableId: table.id, coverCount: 2 },
    });
    const order = await orderResponse.json();

    const lineResponse = await request.post(`${apiBaseUrl}/orders/${order.id}/lines`, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      data: { menuItemId: itemId, quantity: 1, selectedModifierIds: [] },
    });
    expect(lineResponse.status()).toBe(409);
    expect((await lineResponse.json()).code).toBe('catalog.item_unavailable');

    const backAgain = await updateMenuItemAvailabilityResponse(request, itemId, true);
    expect(backAgain.status()).toBe(200);
    expect((await backAgain.json()).isAvailable).toBe(true);

    const menuAfter = await getMenu(request);
    expect(menuAfter.flatMap((c) => c.items).some((i) => i.id === itemId)).toBe(true);

    // Cleanup: the rejected line never landed, so the order still has none
    // — Close requires at least one, so ring up a real (available) item
    // first, same as every other spec that needs to close and free its
    // table. Then remove the test item so it doesn't linger on the menu.
    const realItem = findMenuItem(menuAfter, 'Pão e Azeitonas');
    await request.post(`${apiBaseUrl}/orders/${order.id}/lines`, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      data: { menuItemId: realItem.id, quantity: 1, selectedModifierIds: [] },
    });
    await closeOrderAndClearTable(request, order.id, table.id);
    await deleteMenuItem(request, itemId);
  });

  test('rejects toggling availability for an unknown item', async ({ request }) => {
    const response = await updateMenuItemAvailabilityResponse(request, crypto.randomUUID(), false);
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('catalog.item_not_found');
  });
});
