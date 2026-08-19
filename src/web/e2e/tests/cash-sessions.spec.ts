import { expect, test } from '@playwright/test';
import {
  closeCashSession,
  closeCashSessionResponse,
  createTerminal,
  getCashMovements,
  getCashMovementsResponse,
  getCashSessionResponse,
  getCurrentCashSession,
  getCurrentCashSessionResponse,
  getOrganizations,
  getSites,
  getStaff,
  openCashSession,
  openCashSessionResponse,
  recordCashMovement,
  recordCashMovementResponse,
} from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// Cash sessions (PAY-08) — abertura de caixa, a staff member declaring a
// starting float against a terminal at the start of a shift. Purely a
// record — nothing else in this codebase requires one to exist yet, the
// same "mechanism before the trigger" shape this codebase already uses
// everywhere. Only one open session per terminal at a time, so every test
// here creates its own fresh terminal rather than sharing the seeded demo
// "Caixa 1" (see support/api.ts's own remarks). Cash movements (PAY-09) —
// pay-ins/pay-outs against an open session, always with a reason — build
// on the same session, so their tests live in this same file rather than a
// separate one.

test.describe('cash sessions (PAY-08/09)', () => {
  test('opens a cash session with a float, and GET current returns it', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const demoOrg = organizations[0];
    if (!demoOrg) throw new Error('No seeded organization found for this test.');
    const sites = await getSites(request, demoOrg.id);
    const demoSite = sites[0];
    if (!demoSite) throw new Error('No seeded site found for this test.');
    const staff = await getStaff(request, demoSite.id);
    const staffMember = staff[0];
    if (!staffMember) throw new Error('No seeded staff found for this test.');

    const terminal = await createTerminal(request, demoSite.id, `Cash Test Terminal ${Date.now()}`);

    const session = await openCashSession(request, terminal.id, staffMember.id, 50);
    expect(session.terminalId).toBe(terminal.id);
    expect(session.terminalLabel).toBe(terminal.label);
    expect(session.openedByStaffId).toBe(staffMember.id);
    expect(session.openedByStaffName).toBe(staffMember.name);
    expect(session.openingFloat).toEqual({ amount: 50, currency: 'EUR' });
    expect(session.isOpen).toBe(true);
    expect(session.closedAtUtc).toBeNull();

    const current = await getCurrentCashSession(request, terminal.id);
    expect(current?.id).toBe(session.id);
  });

  test('rejects opening a second session on a terminal that already has one open', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);

    await openCashSession(request, terminal.id, staff[0]!.id, 20);

    const duplicate = await openCashSessionResponse(request, terminal.id, staff[0]!.id, 30);
    expect(duplicate.status()).toBe(409);
    expect((await duplicate.json()).code).toBe('cash_session.already_open');
  });

  test('closes a session — GET current returns null afterward, and closing twice is rejected', async ({
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);

    const session = await openCashSession(request, terminal.id, staff[0]!.id, 10);
    const closed = await closeCashSession(request, session.id);
    expect(closed.isOpen).toBe(false);
    expect(closed.closedAtUtc).not.toBeNull();

    const current = await getCurrentCashSession(request, terminal.id);
    expect(current).toBeNull();

    const doubleClose = await closeCashSessionResponse(request, session.id);
    expect(doubleClose.status()).toBe(400);
    expect((await doubleClose.json()).code).toBe('cash_session.already_closed');

    // Closing freed the terminal up — a fresh session can be opened on it now.
    const reopened = await openCashSession(request, terminal.id, staff[0]!.id, 15);
    expect(reopened.isOpen).toBe(true);
  });

  test('rejects a negative opening float, an unknown terminal, and an unknown staff id', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);

    const negative = await openCashSessionResponse(request, terminal.id, staff[0]!.id, -5);
    expect(negative.status()).toBe(400);
    expect((await negative.json()).code).toBe('cash_session.invalid_opening_float');

    const unknownTerminal = await openCashSessionResponse(
      request,
      '00000000-0000-0000-0000-000000000000',
      staff[0]!.id,
      10,
    );
    expect(unknownTerminal.status()).toBe(404);
    expect((await unknownTerminal.json()).code).toBe('identity.terminal_not_found');

    const unknownStaff = await openCashSessionResponse(
      request,
      terminal.id,
      '00000000-0000-0000-0000-000000000000',
      10,
    );
    expect(unknownStaff.status()).toBe(404);
    expect((await unknownStaff.json()).code).toBe('identity.staff_not_found');
  });

  test('404s closing or fetching an unknown cash session id, and resolving current for an unknown terminal', async ({
    request,
  }) => {
    const unknownId = '00000000-0000-0000-0000-000000000000';

    const closeUnknown = await closeCashSessionResponse(request, unknownId);
    expect(closeUnknown.status()).toBe(404);
    expect((await closeUnknown.json()).code).toBe('cash_session.not_found');

    const getUnknown = await getCashSessionResponse(request, unknownId);
    expect(getUnknown.status()).toBe(404);
    expect((await getUnknown.json()).code).toBe('cash_session.not_found');

    const currentUnknownTerminal = await getCurrentCashSessionResponse(request, unknownId);
    expect(currentUnknownTerminal.status()).toBe(404);
    expect((await currentUnknownTerminal.json()).code).toBe('identity.terminal_not_found');
  });

  test('records a pay-out and a pay-in against an open session, listed oldest first', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 50);

    expect(await getCashMovements(request, session.id)).toEqual([]);

    const payOut = await recordCashMovement(request, session.id, 'PayOut', 15, 'Delivery driver tip', staff[0]!.id);
    expect(payOut.cashSessionId).toBe(session.id);
    expect(payOut.direction).toBe('PayOut');
    expect(payOut.amount).toEqual({ amount: 15, currency: 'EUR' });
    expect(payOut.reason).toBe('Delivery driver tip');
    expect(payOut.recordedByStaffId).toBe(staff[0]!.id);
    expect(payOut.recordedByStaffName).toBe(staff[0]!.name);

    const payIn = await recordCashMovement(request, session.id, 'PayIn', 20, 'Change top-up', staff[0]!.id);
    expect(payIn.direction).toBe('PayIn');

    const movements = await getCashMovements(request, session.id);
    expect(movements).toHaveLength(2);
    expect(movements[0]!.id).toBe(payOut.id);
    expect(movements[1]!.id).toBe(payIn.id);
  });

  test('rejects a movement against a closed session, an invalid direction, a non-positive amount, and an empty reason', async ({
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 50);

    const invalidDirection = await recordCashMovementResponse(request, session.id, 'Sideways', 10, 'x', staff[0]!.id);
    expect(invalidDirection.status()).toBe(400);
    expect((await invalidDirection.json()).code).toBe('cash_movement.invalid_direction');

    const zeroAmount = await recordCashMovementResponse(request, session.id, 'PayOut', 0, 'x', staff[0]!.id);
    expect(zeroAmount.status()).toBe(400);
    expect((await zeroAmount.json()).code).toBe('cash_movement.invalid_amount');

    const emptyReason = await recordCashMovementResponse(request, session.id, 'PayOut', 10, '   ', staff[0]!.id);
    expect(emptyReason.status()).toBe(400);
    expect((await emptyReason.json()).code).toBe('cash_movement.reason_required');

    const unknownStaff = await recordCashMovementResponse(
      request,
      session.id,
      'PayOut',
      10,
      'x',
      '00000000-0000-0000-0000-000000000000',
    );
    expect(unknownStaff.status()).toBe(404);
    expect((await unknownStaff.json()).code).toBe('identity.staff_not_found');

    await closeCashSession(request, session.id);
    const afterClose = await recordCashMovementResponse(request, session.id, 'PayOut', 10, 'x', staff[0]!.id);
    expect(afterClose.status()).toBe(400);
    expect((await afterClose.json()).code).toBe('cash_movement.session_closed');
  });

  test('404s recording or listing movements against an unknown cash session id', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const unknownId = '00000000-0000-0000-0000-000000000000';

    const record = await recordCashMovementResponse(request, unknownId, 'PayOut', 10, 'x', staff[0]!.id);
    expect(record.status()).toBe(404);
    expect((await record.json()).code).toBe('cash_session.not_found');

    const list = await getCashMovementsResponse(request, unknownId);
    expect(list.status()).toBe(404);
    expect((await list.json()).code).toBe('cash_session.not_found');
  });

  test('the admin cash sessions screen opens and closes a session through the real UI', async ({ page, request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const demoSite = sites[0]!;
    const staff = await getStaff(request, demoSite.id);
    const staffMember = staff[0]!;
    const label = `UI Cash Test ${Date.now()}`;
    await createTerminal(request, demoSite.id, label);

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-cash').click();
    await page.waitForSelector('.cash-session-manager');

    const row = page.getByTestId(`cash-session-terminal-${label}`);
    await expect(row).toBeVisible();
    await expect(row).toContainText('Fechada'); // pt default -- CashSessionManager's "closed" badge.

    await row.getByTestId(`cash-session-open-${label}-start`).click();
    await row.getByTestId(`cash-session-staff-${label}`).selectOption(staffMember.id);
    await row.getByTestId(`cash-session-float-${label}`).fill('75');
    await row.getByTestId(`cash-session-open-save-${label}`).click();

    await expect(row.getByTestId(`cash-session-open-${label}`)).toBeVisible();
    await expect(row).toContainText(staffMember.name);

    await row.getByTestId(`cash-session-close-${label}`).click();
    await expect(row.getByTestId(`cash-session-open-${label}`)).not.toBeVisible();
    await expect(row).toContainText('Fechada');
  });

  test('the admin cash movements panel records a pay-out through the real UI', async ({ page, request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const demoSite = sites[0]!;
    const staff = await getStaff(request, demoSite.id);
    const staffMember = staff[0]!;
    const label = `UI Movement Test ${Date.now()}`;
    const terminal = await createTerminal(request, demoSite.id, label);
    // Open via the API — this test is about the movements panel, not the open flow already covered above.
    await openCashSession(request, terminal.id, staffMember.id, 40);

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-cash').click();
    await page.waitForSelector('.cash-session-manager');

    const row = page.getByTestId(`cash-session-terminal-${label}`);
    await expect(row).toBeVisible();
    await expect(row.getByTestId(`cash-session-open-${label}`)).toBeVisible();

    await row.getByTestId(`cash-movement-add-${label}`).click();
    await row.getByTestId(`cash-movement-direction-${label}`).selectOption('PayOut');
    await row.getByTestId(`cash-movement-amount-${label}`).fill('12');
    await row.getByTestId(`cash-movement-reason-${label}`).fill('Petty cash for supplies');
    await row.getByTestId(`cash-movement-staff-${label}`).selectOption(staffMember.id);
    await row.getByTestId(`cash-movement-save-${label}`).click();

    await expect(row.getByTestId(`cash-movement-add-${label}`)).toBeVisible(); // form closed back to the button
    await expect(row).toContainText('Petty cash for supplies');
    await expect(row).toContainText('12,00');
  });
});
