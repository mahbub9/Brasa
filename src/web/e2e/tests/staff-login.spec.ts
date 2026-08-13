import { expect, test } from '@playwright/test';
import { createStaff, getOrganizations, getSites, getStaff, verifyStaffPinResponse } from './support/api';

// WEB-07 — pos's own staff PIN sign-in, unblocked by IDN-08/09 (Staff) and
// IDN-11 (proving the same verify-pin mechanism has a second real caller
// now). Deliberately non-blocking: signing in doesn't gate anything else in
// pos, the same "mechanism before the trigger" shape IDN-08/09 itself
// shipped in — see the feature page for why gating pos behind a login
// screen was deliberately not attempted here.
//
// pos resolves the same "first organization's first site" demo site every
// other single-site screen in this codebase already assumes, so a staff
// member created under an isolated org/site (the usual test-isolation
// pattern) would never actually show up in its picker — every fixture here
// is created directly on the real demo site instead.

async function getDemoSiteId(request: import('@playwright/test').APIRequestContext): Promise<string> {
  const organizations = await getOrganizations(request);
  const org = organizations[0];
  if (!org) throw new Error('No seeded organization found for this test.');
  const sites = await getSites(request, org.id);
  const site = sites[0];
  if (!site) throw new Error('No seeded site found for this test.');
  return site.id;
}

async function getDemoStaff(request: import('@playwright/test').APIRequestContext) {
  return getStaff(request, await getDemoSiteId(request));
}

test.describe('staff PIN sign-in (WEB-07)', () => {
  test('rejects a wrong PIN, then signs in with the correct one; signing out returns to the sign-in button', async ({
    page,
    request,
  }) => {
    const staff = await getDemoStaff(request);
    const manager = staff.find((s) => s.name === 'Ana Ferreira' && s.role === 'Manager');
    if (!manager) throw new Error('Seeded manager "Ana Ferreira" not found — did DevIdentitySeeder change?');

    await page.goto('/');
    await expect(page.getByTestId('staff-sign-in-open')).toBeVisible();

    await page.getByTestId('staff-sign-in-open').click();
    await page.waitForSelector('[data-testid="staff-login-modal"]');
    await page.getByTestId(`staff-login-option-${manager.name}`).click();

    await page.getByTestId('staff-login-pin').fill('0000');
    await page.getByTestId('staff-login-submit').click();
    await expect(page.getByTestId('staff-login-error')).toBeVisible();

    await page.getByTestId('staff-login-pin').fill('1234');
    await page.getByTestId('staff-login-submit').click();

    await expect(page.getByTestId('staff-login-modal')).not.toBeVisible();
    await expect(page.getByTestId('staff-signed-in')).toContainText(manager.name);

    await page.getByTestId('staff-sign-out').click();
    await expect(page.getByTestId('staff-sign-in-open')).toBeVisible();
    await expect(page.getByTestId('staff-signed-in')).not.toBeVisible();
  });

  test('cancelling the login modal leaves the sign-in button, not a signed-in state', async ({ page, request }) => {
    const staff = await getDemoStaff(request);
    if (staff.length === 0) throw new Error('No seeded staff found for this test.');

    await page.goto('/');
    await page.getByTestId('staff-sign-in-open').click();
    await page.waitForSelector('[data-testid="staff-login-modal"]');
    await page.getByTestId('staff-login-cancel').click();

    await expect(page.getByTestId('staff-login-modal')).not.toBeVisible();
    await expect(page.getByTestId('staff-sign-in-open')).toBeVisible();
  });

  test('a locked-out staff member cannot be selected to sign in', async ({ page, request }) => {
    // A fresh staff member on the real demo site (pos's picker only ever
    // shows that one site's staff) -- never the shared seeded "Ana
    // Ferreira"/"Tiago Costa", so locking this one out doesn't disturb any
    // other spec relying on them working.
    const siteId = await getDemoSiteId(request);
    const locked = await createStaff(request, siteId, { name: `Locked Login Test ${Date.now()}`, role: 'Staff', pin: '9999' });

    for (let attempt = 0; attempt < 5; attempt += 1) {
      await verifyStaffPinResponse(request, locked.id, '0000');
    }

    await page.goto('/');
    await page.getByTestId('staff-sign-in-open').click();
    await page.waitForSelector('[data-testid="staff-login-modal"]');

    await expect(page.getByTestId(`staff-login-option-${locked.name}`)).toBeDisabled();
  });
});
