import { expect, test } from '@playwright/test';
import { deleteMenuItem, getMenu, importMenuItemsResponse, updateMenuItemAvailabilityResponse } from './support/api';
import { openAnyFreeTable } from './support/ui';

// ADR 0011 named this a known gap when i18n first shipped: the server's
// ProblemDetails.title is always English (a developer-facing string, not
// localized copy), and pos was showing it verbatim regardless of the
// language toggle — so Portuguese-speaking staff saw English error text.
// describeError() (App.tsx) now looks up a real translation by the stable
// error code first (error.code.<code> in resources/{pt,en}.ts), falling
// back to the raw server message only for codes not yet covered.
//
// Uses a freshly-imported item rather than a shared seeded one, same
// isolation reasoning as menu-availability.spec.ts: toggling availability
// on a seeded item while other specs run concurrently could break an
// unrelated spec's order for a reason that has nothing to do with what it's
// testing.

test.describe('server error messages are localized by error code', () => {
  test('a 409 from adding an unavailable item shows real pt/en text, not raw English either way', async ({
    request,
    page,
  }) => {
    const menu = await getMenu(request);
    const categoryName = menu[0].name;
    const name = `Error Localization Test ${Date.now()}`;
    const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},2.00,0.13\n`;
    const imported = await importMenuItemsResponse(request, csv);
    expect((await imported.json()).created).toBe(1);

    const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
    if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');

    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    // 86 the item after the page has already fetched and rendered the menu
    // — pos doesn't auto-refresh mid-session, so the button below stays
    // clickable even though the server will now reject adding it. This is
    // a real, realistic scenario (another terminal 86s an item while a
    // guest is mid-order on this one), not a contrived one.
    const eightySix = await updateMenuItemAvailabilityResponse(request, item.id, false);
    expect(eightySix.status()).toBe(200);

    const itemButton = page.getByRole('button', { name });
    await itemButton.click();

    const errorBanner = page.getByRole('alert');
    await expect(errorBanner).toBeVisible();
    await expect(errorBanner).toHaveText('Este item está indisponível de momento.×');
    await errorBanner.getByRole('button').click();
    await expect(errorBanner).toBeHidden();

    await page.getByTestId('lang-en').click();
    await itemButton.click();

    await expect(errorBanner).toBeVisible();
    await expect(errorBanner).toHaveText('This item is currently unavailable.×');

    // Cleanup: bring the item back, ring up a real one so the order can
    // close, then remove the test item so it doesn't linger on the menu.
    await updateMenuItemAvailabilityResponse(request, item.id, true);
    await errorBanner.getByRole('button').click();
    await page.getByRole('button', { name: 'Imperial' }).click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Receipt issued' })).toBeVisible();
    await page.getByRole('button', { name: 'Open another table' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
    await deleteMenuItem(request, item.id);
  });
});

// The fallback path — an error code with no error.code.<code> resource
// entry shows the raw server message with the code appended in parentheses
// — isn't covered by a dedicated E2E test: every code pos's own API calls
// (src/api/client.ts) can realistically trigger is translated above, and
// the UI's own client-side guards (min={1} on cover count, maxLength={300}
// on notes, Math.max(1, ...) on split parts) block the ones that would
// otherwise be reachable through the UI. describeError()'s fallback branch
// (App.tsx) is a plain three-line conditional — reviewed, not additionally
// tested, the same bar this codebase already applies to comparably simple
// branches.
