import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  defaultRequiredModifierIds,
  findMenuItem,
  getMenu,
  getOrder,
  openOrderOnAnyFreeTable,
} from './support/api';

// ORD-17 — splitting a bill proportionally by covers per group (a table of
// 5 splitting 2-and-3, not necessarily evenly), reusing Money.Allocate's
// weighted overload directly — the exact case its own remarks call out.
// A pure preview like the even split, so this stays a GET.

test.describe('split by cover', () => {
  test('splits proportionally to covers and sums back to the total', async ({ request }) => {
    const menu = await getMenu(request);
    const frango = findMenuItem(menu, 'Frango na Brasa');
    const imperial = findMenuItem(menu, 'Imperial');

    const { order, table } = await openOrderOnAnyFreeTable(request, 5);
    await addLine(request, order.id, frango.id, 2, defaultRequiredModifierIds(frango));
    const updated = await addLine(request, order.id, imperial.id, 2);
    expect(updated.total.amount).toBeCloseTo(22.6, 2);

    const response = await request.get(`${apiUrl()}/orders/${order.id}/split/by-cover?covers=2&covers=3`);
    expect(response.ok()).toBe(true);
    const shares: { amount: number; currency: string }[] = await response.json();

    expect(shares).toHaveLength(2);
    const sum = shares[0].amount + shares[1].amount;
    expect(sum).toBeCloseTo(updated.total.amount, 2);
    // 2 covers vs 3 covers of 22.60 -> 9.04 and 13.56, exactly (no remainder
    // to distribute, since 22.60 divides evenly five ways to the cent).
    expect(shares[0].amount).toBeCloseTo(9.04, 2);
    expect(shares[1].amount).toBeCloseTo(13.56, 2);

    // Pure preview — the order itself must be unchanged.
    const orderAfter = await getOrder(request, order.id);
    expect(orderAfter.status).toBe('Open');
    expect(orderAfter.total.amount).toBeCloseTo(updated.total.amount, 2);

    await closeOrderAndClearTable(request, order.id, table.id);
  });

  test('rejects empty cover groups, a zero-cover group, and covers that don’t sum to the order', async ({
    request,
  }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 5);
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    await addLine(request, order.id, imperial.id, 1);

    const noCovers = await request.get(`${apiUrl()}/orders/${order.id}/split/by-cover`);
    expect(noCovers.status()).toBe(400);
    expect((await noCovers.json()).code).toBe('order.invalid_split');

    const zeroCover = await request.get(`${apiUrl()}/orders/${order.id}/split/by-cover?covers=0&covers=5`);
    expect(zeroCover.status()).toBe(400);
    expect((await zeroCover.json()).code).toBe('order.invalid_split');

    // Order has 5 covers, this sums to 4.
    const mismatched = await request.get(`${apiUrl()}/orders/${order.id}/split/by-cover?covers=2&covers=2`);
    expect(mismatched.status()).toBe(400);
    expect((await mismatched.json()).code).toBe('order.invalid_split');

    await closeOrderAndClearTable(request, order.id, table.id);
  });
});

function apiUrl(): string {
  return (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';
}
