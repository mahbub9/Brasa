import { expect, test } from '@playwright/test';
import { findMenuItem, getMenu, updateMenuItemDetails, updateMenuItemDetailsResponse } from './support/api';
import { openAnyFreeTable } from './support/ui';

// CAT-02 — a menu item's description and declared allergens. The 14
// allergens are a fixed EU-wide taxonomy (Regulation (EU) No 1169/2011),
// unlike VAT rates, so they're safe to model as a closed enum rather than
// something needing an accountant's confirmation.

test.describe('menu item details', () => {
  test('sets, persists and clears a description and allergen set', async ({ request }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Bife à Portuguesa');

    const updated = await updateMenuItemDetails(request, item.id, 'Bife grelhado com batata frita e ovo a cavalo.', [
      'Eggs',
      'Milk',
    ]);
    expect(updated.description).toBe('Bife grelhado com batata frita e ovo a cavalo.');
    expect(updated.allergens.sort()).toEqual(['Eggs', 'Milk'].sort());

    // Persisted, not just in the response.
    const refetched = findMenuItem(await getMenu(request), 'Bife à Portuguesa');
    expect(refetched.description).toBe('Bife grelhado com batata frita e ovo a cavalo.');
    expect(refetched.allergens.sort()).toEqual(['Eggs', 'Milk'].sort());

    // Replaces the full set — not additive — and null/empty clears both.
    const cleared = await updateMenuItemDetails(request, item.id, null, []);
    expect(cleared.description).toBeNull();
    expect(cleared.allergens).toHaveLength(0);
  });

  test('rejects an unrecognised allergen name and an unknown item', async ({ request }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Bife à Portuguesa');

    const badAllergen = await updateMenuItemDetailsResponse(request, item.id, null, ['Chocolate']);
    expect(badAllergen.status()).toBe(400);
    expect((await badAllergen.json()).code).toBe('catalog.invalid_allergen');

    const unknownItem = await updateMenuItemDetailsResponse(request, crypto.randomUUID(), null, []);
    expect(unknownItem.status()).toBe(404);
    expect((await unknownItem.json()).code).toBe('catalog.item_not_found');
  });

  test('renders the description and allergen tags on the menu screen', async ({ request, page }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Sopa do Dia');
    await updateMenuItemDetails(request, item.id, 'Sopa de legumes da época.', ['Celery']);

    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    const menuItemButton = page.getByRole('button', { name: /Sopa do Dia/ });
    await expect(menuItemButton.locator('.menu-item-description')).toHaveText('Sopa de legumes da época.');
    await expect(menuItemButton.locator('.menu-item-allergens')).toHaveText('Contém: Aipo');

    // Cleanup: ring the item up so the order can close normally, the same
    // way every other UI spec returns its table to the free pool.
    await menuItemButton.click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
  });
});
