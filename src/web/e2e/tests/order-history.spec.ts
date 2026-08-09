import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  findMenuItem,
  getMenu,
  openOrderOnAnyFreeTable,
  searchOrders,
  searchOrdersResponse,
} from './support/api';

// ORD-22 — GET /orders as order history/search. The dev database is NOT
// reset between runs and only 8 tables exist (QA-02's known limitation —
// see docs/development/e2e-testing.md), so a table id filter alone can
// still return other runs' past orders against the same table. Every
// assertion below looks for this test's own order *within* the filtered
// results rather than assuming it is the only one.

test.describe('order history — GET /orders', () => {
  test('finds an order while open, then reflects it as closed with the right total and line count', async ({
    request,
  }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const updated = await addLine(request, order.id, imperial.id, 2);

    const whileOpen = await searchOrders(request, `tableId=${table.id}&status=Open`);
    // Open is a short-lived state one table can only be in once at a time,
    // so unlike the Closed case below this genuinely is the only match.
    expect(whileOpen).toHaveLength(1);
    expect(whileOpen[0].id).toBe(order.id);
    expect(whileOpen[0].lineCount).toBe(1);
    expect(whileOpen[0].closedAtUtc).toBeNull();
    expect(whileOpen[0].total.amount).toBeCloseTo(updated.total.amount, 2);

    await closeOrderAndClearTable(request, order.id, table.id);

    // Same table id, but it's no longer Open — the filtered view must no
    // longer contain it, not still show the now-closed order.
    const afterCloseFilteredOpen = await searchOrders(request, `tableId=${table.id}&status=Open`);
    expect(afterCloseFilteredOpen.some((o) => o.id === order.id)).toBe(false);

    const afterCloseFilteredClosed = await searchOrders(request, `tableId=${table.id}&status=Closed`);
    const closedMatch = afterCloseFilteredClosed.find((o) => o.id === order.id);
    expect(closedMatch, 'expected the just-closed order to appear in the Closed filter').toBeDefined();
    expect(closedMatch?.status).toBe('Closed');
    expect(closedMatch?.closedAtUtc).not.toBeNull();
    expect(closedMatch?.total.amount).toBeCloseTo(updated.total.amount, 2);
  });

  test('rejects an unrecognised status and an out-of-range take', async ({ request }) => {
    const badStatus = await searchOrdersResponse(request, 'status=Cancelled');
    expect(badStatus.status()).toBe(400);
    expect((await badStatus.json()).code).toBe('order.invalid_status_filter');

    const zeroTake = await searchOrdersResponse(request, 'take=0');
    expect(zeroTake.status()).toBe(400);
    expect((await zeroTake.json()).code).toBe('order.invalid_take');

    const hugeTake = await searchOrdersResponse(request, 'take=201');
    expect(hugeTake.status()).toBe(400);
    expect((await hugeTake.json()).code).toBe('order.invalid_take');
  });
});
