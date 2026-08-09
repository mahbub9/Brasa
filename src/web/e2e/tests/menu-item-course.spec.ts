import { expect, test } from '@playwright/test';
import { findMenuItem, getMenu, updateMenuItemCourse, updateMenuItemCourseResponse } from './support/api';

// CAT-14 — which point in the meal a menu item is served at, independent of
// MenuCategory (how the menu is organised for browsing). No pos/admin UI
// yet, and course *firing* (ORD-07) isn't built either — this ships ahead
// of both triggers, the same shape as CAT-13/19. API-only coverage.

test.describe('menu item course', () => {
  test('sets, persists and clears which course an item is served at', async ({ request }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Bife à Portuguesa');

    const updated = await updateMenuItemCourse(request, item.id, 'Main');
    expect(updated.course).toBe('Main');

    // Persisted, not just in the response.
    const refetched = findMenuItem(await getMenu(request), 'Bife à Portuguesa');
    expect(refetched.course).toBe('Main');

    const cleared = await updateMenuItemCourse(request, item.id, null);
    expect(cleared.course).toBeNull();
  });

  test('rejects an unrecognised course name and an unknown item', async ({ request }) => {
    const menu = await getMenu(request);
    const item = findMenuItem(menu, 'Bife à Portuguesa');

    const badCourse = await updateMenuItemCourseResponse(request, item.id, 'MidnightSnack');
    expect(badCourse.status()).toBe(400);
    expect((await badCourse.json()).code).toBe('catalog.invalid_course');

    const unknownItem = await updateMenuItemCourseResponse(request, crypto.randomUUID(), 'Starter');
    expect(unknownItem.status()).toBe(404);
    expect((await unknownItem.json()).code).toBe('catalog.item_not_found');
  });
});
