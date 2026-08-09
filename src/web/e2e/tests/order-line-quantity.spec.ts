import { expect, test } from '@playwright/test';
import {
  addLine,
  clearTable,
  closeOrder,
  closeOrderResponse,
  findMenuItem,
  getMenu,
  openOrderOnAnyFreeTable,
  setLineDiscount,
  setLineQuantity,
  setLineQuantityResponse,
  voidLine,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

// ORD-03 — changing how many of an item were ordered after the line is
// already rung up (a guest asks for one more; the waiter over-rang by
// mistake before anything reached the kitchen). Deliberately not how a
// wrong/unwanted line is undone — that stays void (ORD-10), which requires a
// reason and freezes the line for audit; SetLineQuantity rejects a voided
// line outright rather than letting an edit quietly defeat that trail.

test.describe('order line quantity (ORD-03)', () => {
  test('increasing and decreasing quantity recomputes the line and order totals', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;
    const unitTotal = withLine.lines[0].lineTotal.amount;

    const increased = await setLineQuantity(request, order.id, lineId, 3);
    const increasedLine = increased.lines.find((l) => l.id === lineId)!;
    expect(increasedLine.quantity).toBe(3);
    expect(increasedLine.lineTotal.amount).toBeCloseTo(unitTotal * 3, 2);
    expect(increased.total.amount).toBeCloseTo(unitTotal * 3, 2);

    const decreased = await setLineQuantity(request, order.id, lineId, 1);
    const decreasedLine = decreased.lines.find((l) => l.id === lineId)!;
    expect(decreasedLine.quantity).toBe(1);
    expect(decreasedLine.lineTotal.amount).toBeCloseTo(unitTotal, 2);

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('a percentage discount scales with the new quantity; a fixed discount re-clamps if it would now exceed the smaller gross', async ({
    request,
  }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const withLine = await addLine(request, order.id, imperial.id, 2);
    const lineId = withLine.lines[0].id;

    const discounted = await setLineDiscount(request, order.id, lineId, 'Percentage', 50);
    const grossAt2Units = discounted.lines[0].lineTotal.amount + discounted.lines[0].discountAmount.amount;
    const gross1Unit = grossAt2Units / 2;

    const atOne = await setLineQuantity(request, order.id, lineId, 1);
    const lineAtOne = atOne.lines.find((l) => l.id === lineId)!;
    // Half the smaller (1-unit) gross, not half the original 2-unit gross.
    expect(lineAtOne.discountAmount.amount).toBeCloseTo(gross1Unit * 0.5, 2);

    // Now a fixed discount bigger than the 1-unit gross: SetLineDiscount
    // itself would reject setting it that large directly, so approximate the
    // same "discount now exceeds gross" scenario the domain guards against by
    // discounting at quantity 2, then shrinking to 1.
    await setLineQuantity(request, order.id, lineId, 2);
    const fixedAmount = gross1Unit + 1; // exceeds the 1-unit gross, valid against the 2-unit gross
    await setLineDiscount(request, order.id, lineId, 'FixedAmount', fixedAmount);
    const shrunk = await setLineQuantity(request, order.id, lineId, 1);
    const shrunkLine = shrunk.lines.find((l) => l.id === lineId)!;
    // DiscountAmount is clamped to never exceed the line's own gross.
    expect(shrunkLine.discountAmount.amount).toBeCloseTo(gross1Unit, 2);
    expect(shrunkLine.lineTotal.amount).toBeCloseTo(0, 2);

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('rejects a quantity below 1, an unknown line, a voided line, and a closed order', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const zero = await setLineQuantityResponse(request, order.id, lineId, 0);
    expect(zero.status()).toBe(400);
    expect((await zero.json()).code).toBe('order.invalid_quantity');

    const unknownLine = await setLineQuantityResponse(request, order.id, crypto.randomUUID(), 2);
    expect(unknownLine.status()).toBe(404);
    expect((await unknownLine.json()).code).toBe('order.line_not_found');

    await voidLine(request, order.id, lineId, 'Guest changed their mind');
    const onVoidedLine = await setLineQuantityResponse(request, order.id, lineId, 2);
    expect(onVoidedLine.status()).toBe(409);
    expect((await onVoidedLine.json()).code).toBe('order.line_voided');

    // Order has no non-voided lines left; add one so Close() has something to bill.
    const secondLine = await addLine(request, order.id, imperial.id, 1);
    const secondLineId = secondLine.lines[1].id;
    await closeOrder(request, order.id);
    await clearTable(request, table.id);

    const onClosedOrder = await setLineQuantityResponse(request, order.id, secondLineId, 2);
    expect(onClosedOrder.status()).toBe(409);
    expect((await onClosedOrder.json()).code).toBe('order.not_open');
  });

  test('the +/- stepper in pos increases and decreases a line, disabling "-" at 1', async ({ page }) => {
    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    await page.getByRole('button', { name: 'Imperial' }).click();
    await expect(page.getByText('Ainda sem itens')).toBeHidden();

    const qty = page.locator('.order-line-qty').first();
    const decrease = page.locator('[data-testid^="line-qty-decrease-"]').first();
    const increase = page.locator('[data-testid^="line-qty-increase-"]').first();

    await expect(qty).toHaveText('1×');
    await expect(decrease).toBeDisabled();

    await increase.click();
    await expect(qty).toHaveText('2×');
    await expect(decrease).toBeEnabled();

    await increase.click();
    await expect(qty).toHaveText('3×');

    await decrease.click();
    await expect(qty).toHaveText('2×');
    await decrease.click();
    await expect(qty).toHaveText('1×');
    await expect(decrease).toBeDisabled();

    // Cleanup: close and clear so the table returns to the free pool.
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
  });
});
