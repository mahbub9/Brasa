import { expect, test } from '@playwright/test';
import {
  addLine,
  clearTable,
  closeOrder,
  closeOrderResponse,
  findMenuItem,
  getMenu,
  getPreBill,
  openOrderOnAnyFreeTable,
  voidLine,
  voidLineResponse,
} from './support/api';

// ORD-10 — voiding a line after it's already been rung up, e.g. a dish that
// came out wrong. The line is never deleted (audit trail: what was ordered,
// and why it was cancelled, stays visible) — only its contribution to the
// order's total drops to zero. No pos/admin UI yet — API-only coverage.
// Every void here goes through the real manager-authorisation gate (IDN-11)
// via the seeded demo manager credentials (support/api.ts's
// getDemoManagerCredentials default) — the gate itself is exercised
// separately in manager-authorization.spec.ts.

test.describe('void a line (ORD-10)', () => {
  test('voiding a line zeroes its contribution but keeps it visible for audit', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const withLine = await addLine(request, order.id, imperial.id, 2);
    const lineId = withLine.lines[0].id;
    const lineGrossBefore = withLine.lines[0].lineTotal.amount;
    // A second, never-voided line — closing an order with nothing left to
    // bill is its own case, covered separately below.
    const withSecondLine = await addLine(request, order.id, imperial.id, 1);
    const orderTotalBefore = withSecondLine.total.amount;

    const voided = await voidLine(request, order.id, lineId, 'Guest changed their mind');
    const voidedLine = voided.lines.find((l) => l.id === lineId)!;
    expect(voidedLine.isVoided).toBe(true);
    expect(voidedLine.voidReason).toBe('Guest changed their mind');
    expect(voidedLine.lineTotal.amount).toBe(0);
    // Audit trail: what was ordered stays visible, only its billed value drops.
    expect(voidedLine.quantity).toBe(2);
    expect(voided.total.amount).toBeCloseTo(orderTotalBefore - lineGrossBefore, 2);

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('a voided line disappears from the fiscal document but stays on the pre-bill', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);

    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;
    const secondLine = await addLine(request, order.id, imperial.id, 1);
    const secondLineId = secondLine.lines[1].id;

    const afterVoid = await voidLine(request, order.id, lineId, 'Kitchen ran out');
    const expectedTotal = afterVoid.total.amount;

    const preBill = await getPreBill(request, order.id);
    const preBillLine = preBill.lines.find((l) => l.id === lineId);
    expect(preBillLine?.isVoided).toBe(true);
    const vatSum = preBill.vatBreakdown.reduce((sum, band) => sum + band.grossTotal.amount, 0);
    expect(vatSum).toBeCloseTo(expectedTotal, 2);
    expect(preBill.total.amount).toBeCloseTo(expectedTotal, 2);

    const { order: closedOrder, document } = await closeOrder(request, order.id);
    expect(document.grossTotal.amount).toBeCloseTo(expectedTotal, 2);
    const closedVoidedLine = closedOrder.lines.find((l) => l.id === lineId)!;
    expect(closedVoidedLine.isVoided).toBe(true);
    expect(closedVoidedLine.lineTotal.amount).toBe(0);
    const closedLiveLine = closedOrder.lines.find((l) => l.id === secondLineId)!;
    expect(closedLiveLine.isVoided).toBe(false);

    await clearTable(request, table.id);
  });

  test('rejects a missing reason, an unknown line, a double void, and a closed order', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const noReason = await voidLineResponse(request, order.id, lineId, null);
    expect(noReason.status()).toBe(400);
    expect((await noReason.json()).code).toBe('order.void_reason_required');

    const blankReason = await voidLineResponse(request, order.id, lineId, '   ');
    expect(blankReason.status()).toBe(400);
    expect((await blankReason.json()).code).toBe('order.void_reason_required');

    const unknownLine = await voidLineResponse(request, order.id, crypto.randomUUID(), 'test');
    expect(unknownLine.status()).toBe(404);
    expect((await unknownLine.json()).code).toBe('order.line_not_found');

    await voidLine(request, order.id, lineId, 'first void');
    const doubleVoid = await voidLineResponse(request, order.id, lineId, 'second attempt');
    expect(doubleVoid.status()).toBe(409);
    expect((await doubleVoid.json()).code).toBe('order.line_already_voided');

    // Order has no non-voided lines left; add one so Close() has something to bill.
    const secondLine = await addLine(request, order.id, imperial.id, 1);
    await closeOrder(request, order.id);
    await clearTable(request, table.id);

    const onClosedOrder = await voidLineResponse(request, order.id, secondLine.lines[1].id, 'too late');
    expect(onClosedOrder.status()).toBe(409);
    expect((await onClosedOrder.json()).code).toBe('order.not_open');
  });

  test('closing an order left with no non-voided lines fails at the fiscal layer, order stays open', async ({
    request,
  }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    // Order.Close() itself only checks _lines.Count, which still counts a
    // voided line — but BuildFiscalLines omits every voided line, so a
    // fully-voided order hands the fiscal provider zero lines, and
    // IssueSimplifiedInvoiceAsync's own pre-existing guard rejects that.
    await voidLine(request, order.id, lineId, 'Guest left before ordering anything else');
    const closeAttempt = await closeOrderResponse(request, order.id);
    expect(closeAttempt.status()).toBe(400);
    expect((await closeAttempt.json()).code).toBe('fiscal.no_lines');

    // The rejected close must not have persisted — the order is still Open
    // and can still take a line and close normally afterward.
    const stillOpen = await addLine(request, order.id, imperial.id, 1);
    expect(stillOpen.status).toBe('Open');
    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });
});
