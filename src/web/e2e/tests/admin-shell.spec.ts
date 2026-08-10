import { expect, test } from '@playwright/test';

// WEB-09 — the admin back-office shell. "Visão geral", "Menu" (WEB-10) and,
// since FLR-03's first slice, "Plano de sala" are live; Staff stays a
// labelled placeholder until its own editor lands. This spec proves the
// shell is actually wired to the API (real counts from GET /menu and
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

  test('unbuilt sections are labelled, not silently missing', async ({ page }) => {
    await page.goto(adminBaseUrl);

    await expect(page.getByTestId('nav-staff')).toContainText('Brevemente');
    await expect(page.getByTestId('nav-overview')).not.toContainText('Brevemente');
    await expect(page.getByTestId('nav-menu')).not.toContainText('Brevemente');
    await expect(page.getByTestId('nav-floor')).not.toContainText('Brevemente');
  });
});
