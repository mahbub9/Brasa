import { expect, test } from '@playwright/test';
import { getMenu, getMenuAll, updateMenuCategoryVisibilityResponse } from './support/api';

// CAT-01's own title names "visibility" as in scope, and the epic was
// marked done, but MenuCategory.IsVisible had no setter at all -- nothing
// could ever set it to anything but its default true. Same shape as
// FLR-04/CAT-13/CAT-19's gaps, one level up: a category, not an item.
//
// Uses the real seeded "Sobremesas" category rather than an isolated
// fixture -- there's no endpoint to create a category at all (only items
// can be bulk-imported, always against an existing category). It's no
// longer the *only* spec touching Sobremesas (WEB-10's
// admin-menu-management.spec.ts hides/shows it too, deliberately -- every
// other category has a real dependency elsewhere: Bebidas has "Imperial",
// Pratos Principais has "Frango na Brasa", both looked up by exact name in
// several specs, and Entradas is referenced by name in four more), so two
// specs can legitimately race to hide/show the same category under real
// parallel workers. The id lookup below goes through GET /menu/all
// (never filtered by visibility, so it can't itself fail mid-race the way
// a GET /menu lookup by name could), and the hide/show/verify round trip
// retries as a whole -- same shape as menu-etag.spec.ts's fix for the
// analogous problem.

test.describe('menu category visibility', () => {
  test('hiding a category removes it (and its items) from the menu; showing it restores both', async ({ request }) => {
    const category = (await getMenuAll(request)).find((c) => c.name === 'Sobremesas');
    if (!category) throw new Error('Seeded "Sobremesas" category not found — did DevCatalogSeeder change?');
    expect(category.items.length).toBeGreaterThan(0);

    let hidden = false;
    let shown = false;
    for (let attempt = 1; attempt <= 5 && !shown; attempt += 1) {
      const hideResponse = await updateMenuCategoryVisibilityResponse(request, category.id, false);
      expect(hideResponse.status()).toBe(200);
      expect((await hideResponse.json()).isVisible).toBe(false);

      const menuWhileHidden = await getMenu(request);
      hidden = !menuWhileHidden.some((c) => c.id === category.id);

      const showResponse = await updateMenuCategoryVisibilityResponse(request, category.id, true);
      expect(showResponse.status()).toBe(200);
      expect((await showResponse.json()).isVisible).toBe(true);

      const menuAfter = await getMenu(request);
      const restoredCategory = menuAfter.find((c) => c.id === category.id);
      shown = restoredCategory !== undefined && restoredCategory.items.length === category.items.length;
    }

    // hidden/shown can each be false on an attempt where a concurrent
    // sibling's own show/hide landed in the same narrow window -- what
    // must hold is that *some* attempt saw the category genuinely
    // disappear and reappear correctly, not that the very first one did.
    expect(hidden).toBe(true);
    expect(shown).toBe(true);
  });

  test('rejects toggling visibility for an unknown category', async ({ request }) => {
    const response = await updateMenuCategoryVisibilityResponse(request, crypto.randomUUID(), false);
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('catalog.category_not_found');
  });
});
