import { expect, test } from '@playwright/test';

// WEB-09 — the admin back-office shell. "Visão geral", "Menu" (WEB-10),
// "Plano de sala" (FLR-03) and, since IDN-08/09, "Equipa" (staff) are all
// live now — no placeholder nav entries left. This spec proves the shell
// is actually wired to the API (real counts from GET /menu and
// GET /floor), not a static mock — the same bar applied to pos's own
// screens.

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

test.describe('admin back-office shell', () => {
  test('loads the overview with live counts from the API', async ({ page }) => {
    await page.goto(adminBaseUrl);

    await expect(page.getByText('Brasa')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Visão geral' })).toBeVisible();

    const cards = page.getByTestId('overview-cards');
    await expect(cards).toBeVisible();

    // Real numbers, not placeholders — the seeded dev catalog and floor plan
    // are never empty (see docs/ai/README.md §7 on the 16-table seed).
    await expect(page.getByTestId('overview-categories')).not.toHaveText('0');
    await expect(page.getByTestId('overview-items')).not.toHaveText('0');
    await expect(page.getByTestId('overview-rooms')).not.toHaveText('0');
    await expect(page.getByTestId('overview-tables')).not.toHaveText('0');
  });

  test('every nav section is live, none show a placeholder label', async ({ page }) => {
    await page.goto(adminBaseUrl);

    for (const key of ['overview', 'menu', 'floor', 'staff']) {
      await expect(page.getByTestId(`nav-${key}`)).not.toContainText('Brevemente');
    }

    // The staff nav actually navigates somewhere real, not a static label —
    // seeded staff (IDN-08/09) is never empty, the same "real numbers, not
    // placeholders" bar the overview cards above already hold to.
    await page.getByTestId('nav-staff').click();
    await expect(page.getByTestId(/^staff-/).first()).toBeVisible();
  });
});
