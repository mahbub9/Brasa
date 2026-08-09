import { expect, test } from '@playwright/test';

// Covers the language toggle requested alongside the E2E harness: Portuguese
// is the default (docs/architecture/decisions/0011-i18n.md), English is a
// toggle, and the choice persists in a cookie — checked here at the cookie
// level, not just by re-reading rendered text, since that's the actual
// mechanism being promised (and the seam a future mobile client swaps for
// AsyncStorage/SecureStore).

test.describe('language toggle', () => {
  test('defaults to Portuguese with no cookie set', async ({ page, context }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();
    await expect(page.getByTestId('lang-pt')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByTestId('lang-en')).toHaveAttribute('aria-pressed', 'false');

    const cookies = await context.cookies();
    expect(cookies.find((c) => c.name === 'brasa.lang')).toBeUndefined();
  });

  test('switching to English updates the UI and persists in a cookie, surviving a reload', async ({
    page,
    context,
  }) => {
    await page.goto('/');

    await page.getByTestId('lang-en').click();

    await expect(page.getByRole('heading', { name: 'Choose a table' })).toBeVisible();
    await expect(page.getByTestId('lang-en')).toHaveAttribute('aria-pressed', 'true');

    const cookiesAfterToggle = await context.cookies();
    const langCookie = cookiesAfterToggle.find((c) => c.name === 'brasa.lang');
    expect(langCookie?.value).toBe('en');
    expect(langCookie?.path).toBe('/');
    expect(langCookie?.sameSite).toBe('Lax');
    // Never sent to the API — see the comment on cookieLanguageStore in
    // src/i18n/languageStorage.ts. httpOnly would also make it unreadable by
    // the client-side toggle, which needs to read it back on load.
    expect(langCookie?.httpOnly).toBe(false);

    await page.reload();

    await expect(page.getByRole('heading', { name: 'Choose a table' })).toBeVisible();
    await expect(page.getByTestId('lang-en')).toHaveAttribute('aria-pressed', 'true');

    await page.getByTestId('lang-pt').click();
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();

    const cookiesAfterSwitchBack = await context.cookies();
    expect(cookiesAfterSwitchBack.find((c) => c.name === 'brasa.lang')?.value).toBe('pt');
  });

  test('money keeps pt-PT formatting even when the UI language is English', async ({ page }) => {
    // A total must not change format when staff switch the interface to
    // English — see the comment on formatMoney in src/lib/money.ts. Proven
    // on a real order total: "9,50 €" (comma decimal, trailing €), never
    // "€9.50" — even though every button and label around it reads English.
    await page.goto('/');
    await page.getByTestId('lang-en').click();
    await expect(page.getByRole('heading', { name: 'Choose a table' })).toBeVisible();

    const freeTable = page.locator('.floor-tables button:not([disabled])').first();
    const tableLabel = await freeTable.locator('.floor-table-label').textContent();
    await freeTable.click();
    await page.getByTestId('confirm-open-table').click();

    await page.getByRole('button', { name: 'Frango na Brasa' }).click();
    await expect(page.getByTestId('order-total')).toHaveText(/^9,50\s?€$/);

    // Cleanup: close and clear so the table returns to the free pool for
    // other specs — see support/api.ts's file-level comment.
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Receipt issued' })).toBeVisible();
    await page.getByRole('button', { name: 'Open another table' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
  });
});
