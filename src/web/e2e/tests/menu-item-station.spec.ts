import { expect, test } from '@playwright/test';
import { findMenuItem, getMenu, updateMenuItemStation, updateMenuItemStationResponse } from './support/api';

// CAT-15 — which kitchen station prepares a menu item, independent of both
// MenuCategory (browsing) and Course (CAT-14, when it's served). No
// pos/admin UI yet, and station *routing* (KIT-06) isn't built either —
// ships ahead of both triggers, the same shape as CAT-14. API-only coverage.

test.describe('menu item kitchen station', () => {
  test('sets, persists and clears which station prepares an item', async ({ request }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Bife à Portuguesa');

    const updated = await updateMenuItemStation(request, item.id, 'Grill');
    expect(updated.station).toBe('Grill');

    // Persisted, not just in the response.
    const refetched = findMenuItem(await getMenu(request), 'Bife à Portuguesa');
    expect(refetched.station).toBe('Grill');

    const cleared = await updateMenuItemStation(request, item.id, null);
    expect(cleared.station).toBeNull();
  });

  test('rejects an unrecognised station name and an unknown item', async ({ request }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Bife à Portuguesa');

    const badStation = await updateMenuItemStationResponse(request, item.id, 'Rooftop');
    expect(badStation.status()).toBe(400);
    expect((await badStation.json()).code).toBe('catalog.invalid_station');

    const unknownItem = await updateMenuItemStationResponse(request, crypto.randomUUID(), 'Bar');
    expect(unknownItem.status()).toBe(404);
    expect((await unknownItem.json()).code).toBe('catalog.item_not_found');
  });
});
