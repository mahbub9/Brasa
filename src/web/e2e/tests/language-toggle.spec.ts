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

    await expect(page.getByRole('heading', { name: 'Abrir mesa' })).toBeVisible();
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

    await expect(page.getByRole('heading', { name: 'Open a table' })).toBeVisible();
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

    await expect(page.getByRole('heading', { name: 'Open a table' })).toBeVisible();
    await expect(page.getByTestId('lang-en')).toHaveAttribute('aria-pressed', 'true');

    await page.getByTestId('lang-pt').click();
    await expect(page.getByRole('heading', { name: 'Abrir mesa' })).toBeVisible();

    const cookiesAfterSwitchBack = await context.cookies();
    expect(cookiesAfterSwitchBack.find((c) => c.name === 'brasa.lang')?.value).toBe('pt');
  });

  test('money keeps pt-PT formatting even when the UI language is English', async ({ page }) => {
    // A total must not change format when staff switch the interface to
    // English — see the comment on formatMoney in src/lib/money.ts. Proven
    // here on a freshly opened (zero-total) order: "0,00 €" (comma decimal,
    // trailing €), never "€0.00".
    await page.goto('/');
    await page.getByTestId('lang-en').click();
    await expect(page.getByRole('heading', { name: 'Open a table' })).toBeVisible();

    await page.getByLabel('Table').fill(`Money Format ${Date.now()}`);
    await page.getByRole('button', { name: 'Open table' }).click();

    await expect(page.getByTestId('order-total')).toHaveText(/^0,00\s?€$/);
  });
});
