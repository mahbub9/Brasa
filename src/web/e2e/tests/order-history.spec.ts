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
import type { OrderSummaryDto } from './support/types';

// ORD-22 — GET /orders as order history/search. The dev database is NOT
// reset between runs and only 16 tables exist (QA-02's known limitation —
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

  // API-09 — cursor pagination on GET /orders, the one genuinely unbounded
  // collection in this API today (menu and floor are both small and
  // bounded by the restaurant's own size, so they don't need this yet).
  //
  // Other specs run concurrently against the same dev database (2 workers,
  // fullyParallel), so a plain openedFrom time window can pick up orders
  // this test didn't create — a page can legitimately come back longer
  // than expected. Rather than assert exact page sizes, this walks the
  // full cursor chain and checks only what must be true regardless of that
  // noise: each of this test's own orders appears exactly once across all
  // pages, and the X-Next-Cursor/page-length invariant holds on every page.
  test('paginates via X-Next-Cursor, never repeating or skipping a row', async ({ request }) => {
    const openedFrom = new Date().toISOString();
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');

    const createdIds: string[] = [];
    for (let i = 0; i < 3; i += 1) {
      const { order, table } = await openOrderOnAnyFreeTable(request, 2);
      await addLine(request, order.id, item.id, 1);
      await closeOrderAndClearTable(request, order.id, table.id);
      createdIds.push(order.id);
    }

    const take = 1;
    const seenCounts = new Map<string, number>();
    let cursor: string | undefined;
    let pages = 0;
    const maxPages = 200;

    do {
      const query = `openedFrom=${encodeURIComponent(openedFrom)}&take=${take}${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`;
      const response = await searchOrdersResponse(request, query);
      expect(response.status()).toBe(200);
      const page: OrderSummaryDto[] = await response.json();
      const nextCursor = response.headers()['x-next-cursor'];

      // The invariant SearchOrdersAsync guarantees regardless of what else
      // is in the database: a full page may have more to fetch, a
      // short/empty page never does.
      if (page.length === take) {
        expect(nextCursor).toBeTruthy();
      } else {
        expect(nextCursor).toBeUndefined();
      }

      for (const order of page) {
        seenCounts.set(order.id, (seenCounts.get(order.id) ?? 0) + 1);
      }

      cursor = nextCursor;
      pages += 1;
    } while (cursor && pages < maxPages);

    expect(pages, 'pagination did not terminate within a sane number of pages').toBeLessThan(maxPages);

    for (const id of createdIds) {
      expect(seenCounts.get(id), `expected order ${id} to appear exactly once across all pages`).toBe(1);
    }
  });

  test('rejects a cursor that is not a token GET /orders itself issued', async ({ request }) => {
    const response = await searchOrdersResponse(request, 'cursor=not-a-real-cursor!!!');
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('order.invalid_cursor');
  });
});
