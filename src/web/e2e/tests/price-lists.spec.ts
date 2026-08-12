import { expect, test } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';
import {
  addPriceListEntry,
  addPriceListEntryResponse,
  createOrganization,
  createPriceList,
  createPriceListResponse,
  createSite,
  deleteMenuItem,
  getEffectivePrice,
  getEffectivePriceResponse,
  getMenu,
  getPriceList,
  getPriceListResponse,
  getPriceListsForSite,
  importMenuItemsResponse,
} from './support/api';

// CAT-05 — a narrow first slice, unblocked by IDN-01's Site: price lists
// override a menu item's ordinary price per site. Create/read/add-entry
// only, no rename/delete/remove-entry yet. Nothing in AddLine or either web
// client resolves an effective price through this yet — there is no
// site-selection concept in pos/admin today, so this is verified purely at
// the API level, the same "mechanism before the trigger" shape CAT-14/15
// already established for course/station tags.

async function createTestItem(request: APIRequestContext, price: string): Promise<{ id: string; name: string }> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const name = `Price List Test ${Date.now()}`;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},${price},0.13\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
  return { id: item.id, name };
}

test.describe('price lists (CAT-05)', () => {
  test('creates a price list, adds an entry, and resolves the effective price before and after', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);
    const site = await createSite(request, organization.id, `Test Site ${Date.now()}`, 'Continental');
    const item = await createTestItem(request, '5.00');

    try {
      const priceList = await createPriceList(request, site.id, `Test Price List ${Date.now()}`);
      expect(priceList.siteId).toBe(site.id);
      expect(priceList.entries).toEqual([]);

      // No override yet: resolves to the item's ordinary price.
      const beforeOverride = await getEffectivePrice(request, priceList.id, item.id);
      expect(beforeOverride).toEqual({ menuItemId: item.id, price: { amount: 5, currency: 'EUR' }, isOverridden: false });

      const updated = await addPriceListEntry(request, priceList.id, item.id, 6.5);
      expect(updated.entries).toEqual([{ id: updated.entries[0].id, menuItemId: item.id, price: { amount: 6.5, currency: 'EUR' } }]);

      // Now overridden: resolves to the price list's own price, not the item's.
      const afterOverride = await getEffectivePrice(request, priceList.id, item.id);
      expect(afterOverride).toEqual({ menuItemId: item.id, price: { amount: 6.5, currency: 'EUR' }, isOverridden: true });

      const refetched = await getPriceList(request, priceList.id);
      expect(refetched.entries.length).toBe(1);

      const sitesLists = await getPriceListsForSite(request, site.id);
      expect(sitesLists.some((p) => p.id === priceList.id)).toBe(true);
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });

  test('rejects an empty price list name and an unknown site', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);
    const site = await createSite(request, organization.id, `Test Site ${Date.now()}`, 'Continental');

    const emptyName = await createPriceListResponse(request, site.id, '   ');
    expect(emptyName.status()).toBe(400);
    expect((await emptyName.json()).code).toBe('catalog.invalid_price_list_name');

    const unknownSite = await createPriceListResponse(request, crypto.randomUUID(), 'Some List');
    expect(unknownSite.status()).toBe(404);
    expect((await unknownSite.json()).code).toBe('identity.site_not_found');
  });

  test('rejects a duplicate entry, a negative price, an unknown item, and an unknown price list', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);
    const site = await createSite(request, organization.id, `Test Site ${Date.now()}`, 'Continental');
    const item = await createTestItem(request, '4.00');

    try {
      const priceList = await createPriceList(request, site.id, `Test Price List ${Date.now()}`);
      await addPriceListEntry(request, priceList.id, item.id, 3.5);

      const duplicate = await addPriceListEntryResponse(request, priceList.id, item.id, 4.5);
      expect(duplicate.status()).toBe(409);
      expect((await duplicate.json()).code).toBe('catalog.price_list_entry_exists');

      const other = await createTestItem(request, '2.00');
      try {
        const negative = await addPriceListEntryResponse(request, priceList.id, other.id, -1);
        expect(negative.status()).toBe(400);
        expect((await negative.json()).code).toBe('catalog.invalid_price');
      } finally {
        await deleteMenuItem(request, other.id);
      }

      const unknownItem = await addPriceListEntryResponse(request, priceList.id, crypto.randomUUID(), 1);
      expect(unknownItem.status()).toBe(404);
      expect((await unknownItem.json()).code).toBe('catalog.item_not_found');

      const unknownList = await addPriceListEntryResponse(request, crypto.randomUUID(), item.id, 1);
      expect(unknownList.status()).toBe(404);
      expect((await unknownList.json()).code).toBe('catalog.price_list_not_found');
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });

  test('404s for an unknown price list on GET and on effective-price resolution', async ({ request }) => {
    const unknownGet = await getPriceListResponse(request, crypto.randomUUID());
    expect(unknownGet.status()).toBe(404);
    expect((await unknownGet.json()).code).toBe('catalog.price_list_not_found');

    const unknownEffective = await getEffectivePriceResponse(request, crypto.randomUUID(), crypto.randomUUID());
    expect(unknownEffective.status()).toBe(404);
    expect((await unknownEffective.json()).code).toBe('catalog.price_list_not_found');
  });
});
