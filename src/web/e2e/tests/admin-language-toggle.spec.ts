import { expect, test } from '@playwright/test';

// WEB-09's i18n toggle. Mirrors pos's language-toggle.spec.ts — same
// cookie name (brasa.lang), same pt-default/en-toggle shape (ADR 0011).
// Staff here are not all Portuguese speakers, so English mode must render
// genuine English words for every piece of UI chrome (menu/category names
// stay untranslated real tenant data, same as pos) — checked here against
// actual rendered nav labels, not just that *some* string changed.

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

test.describe('admin language toggle', () => {
  test('defaults to Portuguese with no cookie set', async ({ page, context }) => {
    await page.goto(adminBaseUrl);

    await expect(page.getByRole('heading', { name: 'Visão geral' })).toBeVisible();
    await expect(page.getByTestId('nav-floor')).toContainText('Plano de sala');
    await expect(page.getByTestId('lang-pt')).toHaveAttribute('aria-pressed', 'true');

    const cookies = await context.cookies();
    expect(cookies.find((c) => c.name === 'brasa.lang')).toBeUndefined();
  });

  test('switching to English translates every nav label and card, and persists in a cookie', async ({
    page,
    context,
  }) => {
    await page.goto(adminBaseUrl);

    await page.getByTestId('lang-en').click();

    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
    await expect(page.getByTestId('nav-menu')).toContainText('Menu');
    await expect(page.getByTestId('nav-menu')).not.toContainText('Coming soon');
    await expect(page.getByTestId('nav-floor')).toContainText('Floor plan');
    await expect(page.getByTestId('nav-floor')).not.toContainText('Coming soon');
    await expect(page.getByTestId('nav-staff')).toContainText('Staff');
    await expect(page.getByTestId('nav-staff')).toContainText('Coming soon');
    await expect(page.getByTestId('overview-cards')).toContainText('Menu categories');
    await expect(page.getByTestId('overview-cards')).toContainText('Rooms');
    await expect(page.getByTestId('overview-cards')).toContainText('Tables');

    const cookies = await context.cookies();
    const langCookie = cookies.find((c) => c.name === 'brasa.lang');
    expect(langCookie?.value).toBe('en');
    expect(langCookie?.path).toBe('/');

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  });
});
