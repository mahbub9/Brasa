import { expect, test } from '@playwright/test';
import {
  addLine,
  clearTable,
  closeOrder,
  createOrganization,
  createSite,
  createStaff,
  findMenuItem,
  getDemoManagerCredentials,
  getDemoStaffCredentials,
  getMenu,
  getOrder,
  openOrderOnAnyFreeTable,
  setLineDiscountResponse,
  setOrderDiscountResponse,
  voidLine,
  voidLineResponse,
} from './support/api';

// IDN-11 — the manager-authorisation gate ORD-10 (void) and ORD-11
// (discount) both named as their own deferred, "ships ahead of the trigger"
// gap. Every void/discount request now carries a managerStaffId + PIN,
// verified server-side via the exact same Staff.VerifyPin mechanism
// POST /staff/{id}/verify-pin already uses (IDN-08/09) — same lockout rules,
// checked before the underlying void/discount is ever attempted. This file
// exercises the gate itself; void-line.spec.ts and discounts.spec.ts prove
// the underlying void/discount behaviour, both now going through this same
// gate via the seeded demo manager credentials as their default.

test.describe('manager authorisation for void and discount (IDN-11)', () => {
  test('a non-manager staff credential is rejected without touching the void or the line', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const nonManager = await getDemoStaffCredentials(request);
    const rejected = await voidLineResponse(request, order.id, lineId, 'Wrong person authorising', nonManager);
    expect(rejected.status()).toBe(403);
    expect((await rejected.json()).code).toBe('identity.staff_not_manager');

    // The line must be untouched — authorisation failed before the void's
    // own domain logic was ever reached.
    const stillUnvoided = await getOrder(request, order.id);
    expect(stillUnvoided.lines.find((l) => l.id === lineId)?.isVoided).toBe(false);

    // The real manager succeeds against the same line right after.
    await voidLine(request, order.id, lineId, 'Guest changed their mind');

    // Order.Close() rejects a fully-voided order at the fiscal layer
    // (fiscal.no_lines, ORD-10) — add a second, un-voided line first.
    await addLine(request, order.id, imperial.id, 1);
    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('an unknown manager id and a manager\'s own wrong PIN are both rejected; the correct PIN then still works', async ({
    request,
  }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const manager = await getDemoManagerCredentials(request);

    const unknownManager = await voidLineResponse(request, order.id, lineId, 'test', {
      staffId: crypto.randomUUID(),
      pin: manager.pin,
    });
    expect(unknownManager.status()).toBe(404);
    expect((await unknownManager.json()).code).toBe('identity.staff_not_found');

    const wrongPin = await voidLineResponse(request, order.id, lineId, 'test', {
      staffId: manager.staffId,
      pin: '0000',
    });
    expect(wrongPin.status()).toBe(400);
    expect((await wrongPin.json()).code).toBe('identity.pin_incorrect');

    const voided = await voidLine(request, order.id, lineId, 'Kitchen ran out', manager);
    expect(voided.lines.find((l) => l.id === lineId)?.isVoided).toBe(true);

    // Order.Close() rejects a fully-voided order at the fiscal layer
    // (fiscal.no_lines, ORD-10) — add a second, un-voided line first.
    await addLine(request, order.id, imperial.id, 1);
    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('the same gate covers both line and order discounts', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const nonManager = await getDemoStaffCredentials(request);
    const lineRejected = await setLineDiscountResponse(request, order.id, lineId, 'Percentage', 10, nonManager);
    expect(lineRejected.status()).toBe(403);
    expect((await lineRejected.json()).code).toBe('identity.staff_not_manager');

    const orderRejected = await setOrderDiscountResponse(request, order.id, 'Percentage', 5, {
      staffId: nonManager.staffId,
      pin: '0000',
    });
    // Role is checked before the PIN, so a wrong PIN against a non-manager
    // still surfaces as "not a manager", never "incorrect PIN" — the role
    // check never touches VerifyPin at all.
    expect(orderRejected.status()).toBe(403);
    expect((await orderRejected.json()).code).toBe('identity.staff_not_manager');

    const manager = await getDemoManagerCredentials(request);
    const lineOk = await setLineDiscountResponse(request, order.id, lineId, 'Percentage', 10, manager);
    expect(lineOk.status()).toBe(200);
    const orderOk = await setOrderDiscountResponse(request, order.id, 'FixedAmount', 0.5, manager);
    expect(orderOk.status()).toBe(200);

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('5 consecutive wrong PINs lock the authorising manager out — even their own correct PIN is then refused', async ({
    request,
  }) => {
    // An isolated manager, not the shared seeded "Ana Ferreira" — void-line
    // and discounts specs run in parallel against her own credentials, and
    // locking her out here for 15 minutes would break every one of them.
    const org = await createOrganization(request, `Manager Lockout Org ${Date.now()}`);
    const site = await createSite(request, org.id, 'Manager Lockout Site', 'Continental');
    const isolatedManager = await createStaff(request, site.id, {
      name: 'Isolated Manager',
      role: 'Manager',
      pin: '9999',
    });

    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    // Every attempt below fails authorisation, so the line is never actually
    // touched — the same void/line/order can be reused for all of them.
    for (let attempt = 0; attempt < 5; attempt += 1) {
      const wrongPin = await voidLineResponse(request, order.id, lineId, 'test', {
        staffId: isolatedManager.id,
        pin: '0000',
      });
      expect(wrongPin.status()).toBe(400);
      expect((await wrongPin.json()).code).toBe('identity.pin_incorrect');
    }

    const lockedDespiteCorrectPin = await voidLineResponse(request, order.id, lineId, 'test', {
      staffId: isolatedManager.id,
      pin: '9999',
    });
    expect(lockedDespiteCorrectPin.status()).toBe(409);
    expect((await lockedDespiteCorrectPin.json()).code).toBe('identity.staff_locked');

    // Clean up with a real, unlocked manager rather than waiting out the
    // 15-minute lockout window. Order.Close() rejects a fully-voided order
    // at the fiscal layer (fiscal.no_lines, ORD-10) — add a second, un-voided
    // line first.
    await voidLine(request, order.id, lineId, 'Guest left before ordering anything else');
    await addLine(request, order.id, imperial.id, 1);
    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });
});
