import { expect, test } from '@playwright/test';
import {
  createOrganization,
  createSite,
  createStaff,
  createStaffResponse,
  getOrganizations,
  getSites,
  getStaff,
  setStaffPin,
  setStaffPinResponse,
  verifyStaffPin,
  verifyStaffPinResponse,
} from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// Staff PIN accounts (IDN-08/09) — a narrow first slice. PIN hashing
// (PBKDF2), lockout after 5 consecutive incorrect PINs, and rotation are
// real, live mechanisms; verifying a *known* staff id's PIN, not "identify
// me by PIN alone with no picker" (see Staff's own remarks for why).
// Deliberately not wired into any endpoint's own authorization decision —
// ORD-10/ORD-11 both still have "no manager-authorisation gate yet" as
// their own named, deferred gap.

test.describe('staff PIN accounts (IDN-08/09)', () => {
  test('verifies a correct PIN, rejects an incorrect one, locks out after 5 failures, and a reset clears it', async ({
    request,
  }) => {
    const org = await createOrganization(request, `Staff Lockout Org ${Date.now()}`);
    const site = await createSite(request, org.id, 'Staff Lockout Site', 'Continental');
    const staff = await createStaff(request, site.id, { name: 'Lockout Test', role: 'Manager', pin: '1234' });
    expect(staff.role).toBe('Manager');
    expect(staff.isLocked).toBe(false);

    const verified = await verifyStaffPin(request, staff.id, '1234');
    expect(verified.isLocked).toBe(false);

    const wrongPin = await verifyStaffPinResponse(request, staff.id, '0000');
    expect(wrongPin.status()).toBe(400);
    expect((await wrongPin.json()).code).toBe('identity.pin_incorrect');

    // 4 more wrong attempts -- 5 total -- triggers the lockout.
    for (let attempt = 0; attempt < 4; attempt += 1) {
      await verifyStaffPinResponse(request, staff.id, '0000');
    }

    // Even the *correct* PIN is refused once locked.
    const lockedDespiteCorrectPin = await verifyStaffPinResponse(request, staff.id, '1234');
    expect(lockedDespiteCorrectPin.status()).toBe(409);
    expect((await lockedDespiteCorrectPin.json()).code).toBe('identity.staff_locked');

    const staffList = await getStaff(request, site.id);
    expect(staffList.find((s) => s.id === staff.id)?.isLocked).toBe(true);

    const afterReset = await setStaffPin(request, staff.id, '5678');
    expect(afterReset.isLocked).toBe(false);

    const verifiedAfterReset = await verifyStaffPin(request, staff.id, '5678');
    expect(verifiedAfterReset.isLocked).toBe(false);
  });

  test('rejects an empty name, a malformed PIN, an unrecognised role, and unknown ids', async ({ request }) => {
    const org = await createOrganization(request, `Staff Validation Org ${Date.now()}`);
    const site = await createSite(request, org.id, 'Staff Validation Site', 'Continental');

    const emptyName = await createStaffResponse(request, site.id, { name: '', role: 'Staff', pin: '1234' });
    expect(emptyName.status()).toBe(400);
    expect((await emptyName.json()).code).toBe('identity.invalid_staff_name');

    const badRole = await createStaffResponse(request, site.id, {
      name: 'X',
      role: 'Owner' as unknown as 'Staff',
      pin: '1234',
    });
    expect(badRole.status()).toBe(400);
    expect((await badRole.json()).code).toBe('identity.invalid_staff_role');

    const shortPin = await createStaffResponse(request, site.id, { name: 'X', role: 'Staff', pin: '12' });
    expect(shortPin.status()).toBe(400);
    expect((await shortPin.json()).code).toBe('identity.invalid_pin');

    const longPin = await createStaffResponse(request, site.id, { name: 'X', role: 'Staff', pin: '1234567' });
    expect(longPin.status()).toBe(400);
    expect((await longPin.json()).code).toBe('identity.invalid_pin');

    const nonDigitPin = await createStaffResponse(request, site.id, { name: 'X', role: 'Staff', pin: 'abcd' });
    expect(nonDigitPin.status()).toBe(400);
    expect((await nonDigitPin.json()).code).toBe('identity.invalid_pin');

    const unknownSite = await createStaffResponse(request, crypto.randomUUID(), {
      name: 'X',
      role: 'Staff',
      pin: '1234',
    });
    expect(unknownSite.status()).toBe(404);
    expect((await unknownSite.json()).code).toBe('identity.site_not_found');

    const unknownStaffVerify = await verifyStaffPinResponse(request, crypto.randomUUID(), '1234');
    expect(unknownStaffVerify.status()).toBe(404);
    expect((await unknownStaffVerify.json()).code).toBe('identity.staff_not_found');

    const unknownStaffSetPin = await setStaffPinResponse(request, crypto.randomUUID(), '1234');
    expect(unknownStaffSetPin.status()).toBe(404);
    expect((await unknownStaffSetPin.json()).code).toBe('identity.staff_not_found');

    const valid = await createStaff(request, site.id, { name: 'Valid', role: 'Staff', pin: '1234' });
    const badNewPin = await setStaffPinResponse(request, valid.id, 'xx');
    expect(badNewPin.status()).toBe(400);
    expect((await badNewPin.json()).code).toBe('identity.invalid_pin');
  });

  test('the admin staff screen adds a staff member and resets their PIN, both taking effect for real', async ({
    page,
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const demoOrg = organizations[0];
    if (!demoOrg) throw new Error('No seeded organization found for this test.');
    const sites = await getSites(request, demoOrg.id);
    const demoSite = sites[0];
    if (!demoSite) throw new Error('No seeded site found for this test.');

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-staff').click();
    await page.waitForSelector('.staff-manager');

    const name = `UI Staff Test ${Date.now()}`;
    await page.getByTestId('add-staff').click();
    await page.getByTestId('new-staff-name').fill(name);
    await page.getByTestId('new-staff-role').selectOption('Manager');
    await page.getByTestId('new-staff-pin').fill('2468');
    await page.getByTestId('new-staff-save').click();

    const row = page.getByTestId(`staff-${name}`);
    await expect(row).toBeVisible();
    await expect(row).toContainText('Gerente'); // pt default -- StaffRole "Manager".

    await row.getByTestId(`staff-reset-pin-${name}`).click();
    await row.getByTestId(`staff-new-pin-${name}`).fill('1357');
    await row.getByTestId(`staff-save-pin-${name}`).click();
    // Reset form closes back to the button -- confirms the save round-tripped.
    await expect(row.getByTestId(`staff-reset-pin-${name}`)).toBeVisible();

    // Not just "the UI showed no error" -- the new PIN actually works.
    const staffList = await getStaff(request, demoSite.id);
    const created = staffList.find((s) => s.name === name);
    if (!created) throw new Error(`"${name}" not found via GET /sites/{id}/staff after creating it through the UI.`);

    const verified = await verifyStaffPin(request, created.id, '1357');
    expect(verified.isLocked).toBe(false);
  });
});
