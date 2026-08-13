import { expect, test } from '@playwright/test';
import {
  addLine,
  clearTable,
  closeOrder,
  findMenuItem,
  getMenu,
  getPreBill,
  openOrderOnAnyFreeTable,
  setLineDiscount,
  setLineDiscountResponse,
  setOrderDiscount,
  setOrderDiscountResponse,
} from './support/api';

// ORD-11 — line and order-level discounts, percentage and fixed. No pos/admin
// UI yet (ships ahead of the trigger, same shape as ORD-13/14/CAT-13/19) —
// API-only coverage. Every discount call here goes through the real
// manager-authorisation gate (IDN-11) via the seeded demo manager
// credentials (support/api.ts's getDemoManagerCredentials default) — the
// gate itself is exercised separately in manager-authorization.spec.ts.

test.describe('order and line discounts (ORD-11)', () => {
  test('a line discount reduces only that line, an order discount reduces the subtotal on top', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const withLines = await addLine(request, order.id, imperial.id, 2);
    const lineId = withLines.lines[0].id;
    const grossBefore = withLines.total.amount;
    const lineGrossBefore = withLines.lines[0].lineTotal.amount;

    const afterLineDiscount = await setLineDiscount(request, order.id, lineId, 'Percentage', 10);
    const line = afterLineDiscount.lines.find((l) => l.id === lineId)!;
    expect(line.discountType).toBe('Percentage');
    expect(line.discountValue).toBe(10);
    // 10% off this line only, reflected both on the line and rolled up into
    // order.total.
    expect(line.discountAmount.amount).toBeGreaterThan(0);
    expect(line.lineTotal.amount).toBeCloseTo(lineGrossBefore - line.discountAmount.amount, 2);
    expect(afterLineDiscount.total.amount).toBeLessThan(grossBefore);

    const subtotalAfterLineDiscount = afterLineDiscount.total.amount;
    const afterOrderDiscount = await setOrderDiscount(request, order.id, 'FixedAmount', 1);
    expect(afterOrderDiscount.discountType).toBe('FixedAmount');
    expect(afterOrderDiscount.discountValue).toBe(1);
    expect(afterOrderDiscount.discountAmount.amount).toBeCloseTo(1, 2);
    expect(afterOrderDiscount.total.amount).toBeCloseTo(subtotalAfterLineDiscount - 1, 2);

    // The pre-bill (ORD-18) must show the exact same total a discounted order
    // carries — same invariant ORD-19 already established for reprints, now
    // proven with a discount in the mix too.
    const preBill = await getPreBill(request, order.id);
    expect(preBill.total.amount).toBeCloseTo(afterOrderDiscount.total.amount, 2);
    const vatSum = preBill.vatBreakdown.reduce((sum, band) => sum + band.grossTotal.amount, 0);
    expect(vatSum).toBeCloseTo(afterOrderDiscount.total.amount, 2);

    // And the fiscal document issued at close must reconcile to the cent
    // against order.Total with the discount applied — the exact invariant
    // CloseOrderAsync's own comment calls out, now exercised with a discount.
    const { order: closedOrder, document } = await closeOrder(request, order.id);
    expect(document.grossTotal.amount).toBeCloseTo(closedOrder.total.amount, 2);
    expect(closedOrder.total.amount).toBeCloseTo(afterOrderDiscount.total.amount, 2);

    await clearTable(request, table.id);
  });

  test('clearing a discount restores the undiscounted total', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;
    const originalLineTotal = withLine.lines[0].lineTotal.amount;

    await setLineDiscount(request, order.id, lineId, 'FixedAmount', 0.5);
    const cleared = await setLineDiscount(request, order.id, lineId, null, null);
    const clearedLine = cleared.lines.find((l) => l.id === lineId)!;
    expect(clearedLine.discountType).toBeNull();
    expect(clearedLine.discountValue).toBeNull();
    expect(clearedLine.discountAmount.amount).toBe(0);
    expect(clearedLine.lineTotal.amount).toBeCloseTo(originalLineTotal, 2);

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('rejects a malformed, out-of-range, oversized, or partial discount request', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;
    const lineGross = withLine.lines[0].lineTotal.amount;

    const unknownType = await setLineDiscountResponse(request, order.id, lineId, 'HalfOff', 10);
    expect(unknownType.status()).toBe(400);
    expect((await unknownType.json()).code).toBe('order.invalid_discount');

    const tooHighPercent = await setLineDiscountResponse(request, order.id, lineId, 'Percentage', 101);
    expect(tooHighPercent.status()).toBe(400);
    expect((await tooHighPercent.json()).code).toBe('order.invalid_discount');

    const zeroPercent = await setLineDiscountResponse(request, order.id, lineId, 'Percentage', 0);
    expect(zeroPercent.status()).toBe(400);
    expect((await zeroPercent.json()).code).toBe('order.invalid_discount');

    const oversizedFixed = await setLineDiscountResponse(request, order.id, lineId, 'FixedAmount', lineGross + 1);
    expect(oversizedFixed.status()).toBe(400);
    expect((await oversizedFixed.json()).code).toBe('order.invalid_discount');

    const onlyType = await setOrderDiscountResponse(request, order.id, 'Percentage', null);
    expect(onlyType.status()).toBe(400);
    expect((await onlyType.json()).code).toBe('order.invalid_discount');

    const onlyValue = await setOrderDiscountResponse(request, order.id, null, 5);
    expect(onlyValue.status()).toBe(400);
    expect((await onlyValue.json()).code).toBe('order.invalid_discount');

    // None of the rejected calls should have changed anything.
    const unchanged = await setLineDiscountResponse(request, order.id, lineId, null, null);
    expect((await unchanged.json()).lines.find((l: { id: string }) => l.id === lineId).lineTotal.amount).toBeCloseTo(
      lineGross,
      2,
    );

    const unknownLine = await setLineDiscountResponse(request, order.id, crypto.randomUUID(), 'Percentage', 10);
    expect(unknownLine.status()).toBe(404);
    expect((await unknownLine.json()).code).toBe('order.line_not_found');

    await closeOrder(request, order.id);
    await clearTable(request, table.id);

    const onClosedOrder = await setOrderDiscountResponse(request, order.id, 'Percentage', 10);
    expect(onClosedOrder.status()).toBe(409);
    expect((await onClosedOrder.json()).code).toBe('order.not_open');
  });
});
