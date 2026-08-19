import { expect, test } from '@playwright/test';
import {
  addLine,
  clearTable,
  closeCashSession,
  closeCashSessionResponse,
  closeOrder,
  createTerminal,
  findMenuItem,
  getCashMovements,
  getCashMovementsResponse,
  getCashSessionResponse,
  getCashSessionVariance,
  getCashSessionVarianceResponse,
  getCurrentCashSession,
  getCurrentCashSessionResponse,
  getMenu,
  getOrganizations,
  getSites,
  getStaff,
  openCashSession,
  openCashSessionResponse,
  openOrderOnAnyFreeTable,
  openTakeawayOrder,
  recordCashCount,
  recordCashCountResponse,
  recordCashMovement,
  recordCashMovementResponse,
  recordPayment,
  recordPaymentResponse,
  recordSplitPayment,
} from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// Cash sessions (PAY-08) — abertura de caixa, a staff member declaring a
// starting float against a terminal at the start of a shift. Purely a
// record — nothing else in this codebase requires one to exist yet, the
// same "mechanism before the trigger" shape this codebase already uses
// everywhere. Only one open session per terminal at a time, so every test
// here creates its own fresh terminal rather than sharing the seeded demo
// "Caixa 1" (see support/api.ts's own remarks). Cash movements (PAY-09) —
// pay-ins/pay-outs against an open session, always with a reason — and a
// blind cash count (PAY-10) both build on the same session, so their tests
// live in this same file rather than a separate one.

test.describe('cash sessions (PAY-08/09/10)', () => {
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

  test('records a blind cash count against an open session, resolving the counting staff member', async ({
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 50);
    expect(session.countedAmount).toBeNull();

    const counted = await recordCashCount(request, session.id, staff[0]!.id, 62.5);
    expect(counted.countedAmount).toEqual({ amount: 62.5, currency: 'EUR' });
    expect(counted.countedByStaffId).toBe(staff[0]!.id);
    expect(counted.countedByStaffName).toBe(staff[0]!.name);
    expect(counted.countedAtUtc).not.toBeNull();

    // GET reflects the same recorded count, not just the response body of the record call.
    const fetched = await getCashSessionResponse(request, session.id);
    const fetchedBody = await fetched.json();
    expect(fetchedBody.countedAmount).toEqual({ amount: 62.5, currency: 'EUR' });
  });

  test('rejects a second count on the same session, a negative amount, an unknown staff id, and a count on a closed session', async ({
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 50);

    await recordCashCount(request, session.id, staff[0]!.id, 50);

    const recount = await recordCashCountResponse(request, session.id, staff[0]!.id, 45);
    expect(recount.status()).toBe(400);
    expect((await recount.json()).code).toBe('cash_session.already_counted');

    const terminal2 = await createTerminal(request, sites[0]!.id, `Cash Test Terminal ${Date.now()}-2`);
    const session2 = await openCashSession(request, terminal2.id, staff[0]!.id, 30);

    const negative = await recordCashCountResponse(request, session2.id, staff[0]!.id, -5);
    expect(negative.status()).toBe(400);
    expect((await negative.json()).code).toBe('cash_session.invalid_counted_amount');

    const unknownStaff = await recordCashCountResponse(
      request,
      session2.id,
      '00000000-0000-0000-0000-000000000000',
      30,
    );
    expect(unknownStaff.status()).toBe(404);
    expect((await unknownStaff.json()).code).toBe('identity.staff_not_found');

    await closeCashSession(request, session2.id);
    const onClosed = await recordCashCountResponse(request, session2.id, staff[0]!.id, 30);
    expect(onClosed.status()).toBe(400);
    expect((await onClosed.json()).code).toBe('cash_session.already_closed');
  });

  test('404s recording a blind count against an unknown cash session id', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);

    const response = await recordCashCountResponse(
      request,
      '00000000-0000-0000-0000-000000000000',
      staff[0]!.id,
      10,
    );
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('cash_session.not_found');
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

  test('the admin blind count panel records a count through the real UI, then collapses to read-only', async ({
    page,
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const demoSite = sites[0]!;
    const staff = await getStaff(request, demoSite.id);
    const staffMember = staff[0]!;
    const label = `UI Count Test ${Date.now()}`;
    const terminal = await createTerminal(request, demoSite.id, label);
    await openCashSession(request, terminal.id, staffMember.id, 40);

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-cash').click();
    await page.waitForSelector('.cash-session-manager');

    const row = page.getByTestId(`cash-session-terminal-${label}`);
    await expect(row).toBeVisible();
    await expect(row.getByTestId(`cash-count-start-${label}`)).toBeVisible();

    await row.getByTestId(`cash-count-start-${label}`).click();
    await row.getByTestId(`cash-count-staff-${label}`).selectOption(staffMember.id);
    await row.getByTestId(`cash-count-amount-${label}`).fill('55.5');
    await row.getByTestId(`cash-count-save-${label}`).click();

    await expect(row.getByTestId(`cash-count-recorded-${label}`)).toBeVisible();
    await expect(row).toContainText('55,50');
    await expect(row).toContainText(staffMember.name);
    // The "record count" trigger is gone -- a blind count is at most once per session.
    await expect(row.getByTestId(`cash-count-start-${label}`)).not.toBeVisible();
  });
});

// Fecho de caixa variance (PAY-11) — a computed report comparing what should
// be in the drawer (opening float + pay-ins - pay-outs + cash payments taken
// while this session was open) against what a blind count (PAY-10) actually
// found. Never stored — recomputed from Payment.CashSessionId and
// CashMovement rows on every request, so it stays correct even if a payment
// or movement is recorded after the fact. This is the sharper starting point
// PAY-10's own docs flagged: PAY-10 could record a count, but had nothing to
// compare it against until a payment could carry a CashSessionId at all.

test.describe('fecho de caixa variance (PAY-11)', () => {
  test('computes the expected amount from float + movements + cash payments, variance null until a count exists', async ({
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Variance Test ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 50);

    await recordCashMovement(request, session.id, 'PayIn', 20, 'Change top-up', staff[0]!.id);
    await recordCashMovement(request, session.id, 'PayOut', 5, 'Delivery driver tip', staff[0]!.id);

    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 4); // 4 x 2.50 = 10.00
    const payment = await recordPayment(request, order.id, 'Cash', 10, { cashSessionId: session.id });
    expect(payment.cashSessionId).toBe(session.id);

    const beforeCount = await getCashSessionVariance(request, session.id);
    expect(beforeCount.openingFloat).toEqual({ amount: 50, currency: 'EUR' });
    expect(beforeCount.totalPayIns).toEqual({ amount: 20, currency: 'EUR' });
    expect(beforeCount.totalPayOuts).toEqual({ amount: 5, currency: 'EUR' });
    expect(beforeCount.totalCashPaymentsTaken).toEqual({ amount: 10, currency: 'EUR' });
    expect(beforeCount.expectedAmount).toEqual({ amount: 75, currency: 'EUR' });
    expect(beforeCount.countedAmount).toBeNull();
    expect(beforeCount.variance).toBeNull();

    await recordCashCount(request, session.id, staff[0]!.id, 73);
    const afterCount = await getCashSessionVariance(request, session.id);
    expect(afterCount.expectedAmount).toEqual({ amount: 75, currency: 'EUR' });
    expect(afterCount.countedAmount).toEqual({ amount: 73, currency: 'EUR' });
    expect(afterCount.variance).toEqual({ amount: -2, currency: 'EUR' });

    // Still computable after the session itself is closed.
    await closeCashSession(request, session.id);
    const afterClose = await getCashSessionVariance(request, session.id);
    expect(afterClose.variance).toEqual({ amount: -2, currency: 'EUR' });

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('a card tender does not count toward totalCashPaymentsTaken, only cash does', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Variance Card Test ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 0);

    const order = await openTakeawayOrder(request, `Variance card test ${Date.now()}`);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 2); // 5.00 due

    await recordPayment(request, order.id, 'Card', 5, { cashSessionId: session.id });

    const variance = await getCashSessionVariance(request, session.id);
    expect(variance.totalCashPaymentsTaken).toEqual({ amount: 0, currency: 'EUR' });
    expect(variance.expectedAmount).toEqual({ amount: 0, currency: 'EUR' });

    await closeOrder(request, order.id);
  });

  test('a split payment attributes its cash tender to the session, ignoring the card tender', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const staff = await getStaff(request, sites[0]!.id);
    const terminal = await createTerminal(request, sites[0]!.id, `Variance Split Test ${Date.now()}`);
    const session = await openCashSession(request, terminal.id, staff[0]!.id, 0);

    const order = await openTakeawayOrder(request, `Variance split test ${Date.now()}`);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 4); // 10.00 due

    const tenders = await recordSplitPayment(
      request,
      order.id,
      [
        { method: 'Cash', amountTendered: 6 },
        { method: 'Card', amountTendered: 4 },
      ],
      session.id,
    );
    expect(tenders.every((t) => t.cashSessionId === session.id)).toBe(true);

    const variance = await getCashSessionVariance(request, session.id);
    expect(variance.totalCashPaymentsTaken).toEqual({ amount: 6, currency: 'EUR' });
    expect(variance.expectedAmount).toEqual({ amount: 6, currency: 'EUR' });

    await closeOrder(request, order.id);
  });

  test('rejects a payment against an unknown cash session id, leaving the order unpaid', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 2); // 5.00 due

    const rejected = await recordPaymentResponse(request, order.id, 'Cash', 5, {
      cashSessionId: '00000000-0000-0000-0000-000000000000',
    });
    expect(rejected.status()).toBe(404);
    expect((await rejected.json()).code).toBe('cash_session.not_found');

    // The rejected tender left nothing behind -- a following ordinary payment still owes the full amount.
    const payment = await recordPayment(request, order.id, 'Cash', 5);
    expect(payment.amountApplied).toEqual({ amount: 5, currency: 'EUR' });
    expect(payment.remainingBalance).toEqual({ amount: 0, currency: 'EUR' });

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('404s the variance endpoint for an unknown cash session id', async ({ request }) => {
    const response = await getCashSessionVarianceResponse(request, '00000000-0000-0000-0000-000000000000');
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('cash_session.not_found');
  });

  const adminBaseUrlForVariance = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

  test('the admin variance report appears after a blind count is recorded through the real UI', async ({
    page,
    request,
  }) => {
    const organizations = await getOrganizations(request);
    const sites = await getSites(request, organizations[0]!.id);
    const demoSite = sites[0]!;
    const staff = await getStaff(request, demoSite.id);
    const staffMember = staff[0]!;
    const label = `UI Variance Test ${Date.now()}`;
    const terminal = await createTerminal(request, demoSite.id, label);
    const session = await openCashSession(request, terminal.id, staffMember.id, 40);

    await page.goto(adminBaseUrlForVariance);
    await page.getByTestId('nav-cash').click();
    await page.waitForSelector('.cash-session-manager');

    const row = page.getByTestId(`cash-session-terminal-${label}`);
    await expect(row).not.toContainText('Esperado na caixa'); // no report before a count exists

    await row.getByTestId(`cash-count-start-${label}`).click();
    await row.getByTestId(`cash-count-staff-${label}`).selectOption(staffMember.id);
    await row.getByTestId(`cash-count-amount-${label}`).fill('38');
    await row.getByTestId(`cash-count-save-${label}`).click();

    await expect(row.getByTestId(`cash-variance-${label}`)).toBeVisible();
    await expect(row).toContainText('Esperado na caixa: 40,00');
    await expect(row.getByTestId(`cash-variance-badge-${label}`)).toContainText('Diferença: -2,00');

    await closeCashSession(request, session.id);
  });
});
