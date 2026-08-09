import { expect, test } from '@playwright/test';
import { getMenu, updateMenuCategoryVisibilityResponse } from './support/api';

// CAT-01's own title names "visibility" as in scope, and the epic was
// marked done, but MenuCategory.IsVisible had no setter at all -- nothing
// could ever set it to anything but its default true. Same shape as
// FLR-04/CAT-13/CAT-19's gaps, one level up: a category, not an item.
//
// Uses the real seeded "Sobremesas" category rather than an isolated
// fixture -- there's no endpoint to create a category at all (only items
// can be bulk-imported, always against an existing category), and no other
// spec references Sobremesas or its items, so hiding it briefly carries no
// real collision risk under parallel workers.

test.describe('menu category visibility', () => {
  test('hiding a category removes it (and its items) from the menu; showing it restores both', async ({ request }) => {
    const menuBefore = await getMenu(request);
    const category = menuBefore.find((c) => c.name === 'Sobremesas');
    if (!category) throw new Error('Seeded "Sobremesas" category not found — did DevCatalogSeeder change?');
    expect(category.items.length).toBeGreaterThan(0);

    const hideResponse = await updateMenuCategoryVisibilityResponse(request, category.id, false);
    expect(hideResponse.status()).toBe(200);
    const hideBody = await hideResponse.json();
    expect(hideBody.isVisible).toBe(false);

    const menuWhileHidden = await getMenu(request);
    expect(menuWhileHidden.some((c) => c.id === category.id)).toBe(false);

    const showResponse = await updateMenuCategoryVisibilityResponse(request, category.id, true);
    expect(showResponse.status()).toBe(200);
    expect((await showResponse.json()).isVisible).toBe(true);

    const menuAfter = await getMenu(request);
    const restoredCategory = menuAfter.find((c) => c.id === category.id);
    expect(restoredCategory).toBeDefined();
    expect(restoredCategory?.items.length).toBe(category.items.length);
  });

  test('rejects toggling visibility for an unknown category', async ({ request }) => {
    const response = await updateMenuCategoryVisibilityResponse(request, crypto.randomUUID(), false);
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('catalog.category_not_found');
  });
});
