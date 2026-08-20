import { expect, test } from '@playwright/test';
import { addLine, clearTable, closeOrderResponse, findMenuItem, getMenu, openOrderOnAnyFreeTable } from './support/api';

// API-16/17 — this codebase's first realtime channel. FloorHub broadcasts a
// payload-less "FloorChanged" signal whenever any table's state changes;
// pos re-fetches GET /floor (the REST equivalent hard rule 7 requires) on
// receiving it, rather than trusting a push payload. Proven here by never
// reloading the watching tab — only a live push, not a poll from the test
// itself, can make either assertion below resolve.

test.describe('floor realtime updates (API-16/17)', () => {
  test('opening a table from a second tab flips it to Occupied in the first, with no reload', async ({
    browser,
    request,
  }) => {
    const watcher = await browser.newPage();
    await watcher.goto('/');
    await watcher.waitForSelector('.table-picker');

    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const watchedTableRow = watcher.locator('.floor-table', { has: watcher.getByTestId(`table-${table.label}`) });
    // No watcher.reload() anywhere above this line. Occupied tables stay
    // clickable (re-entering the order to add lines/issue the bill), so
    // the pushed state shows up as a class change, not a disabled button.
    await expect(watchedTableRow).toHaveClass(/floor-table-Occupied/, { timeout: 10_000 });

    const imperial = findMenuItem(await getMenu(request), 'Imperial');
    await addLine(request, order.id, imperial.id, 1);
    await closeOrderResponse(request, order.id);
    await clearTable(request, table.id);
    await watcher.close();
  });

  test('clearing a dirty table from a second tab flips it back to Free in the first, with no reload', async ({
    browser,
    request,
  }) => {
    const imperial = findMenuItem(await getMenu(request), 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    await addLine(request, order.id, imperial.id, 1);
    await closeOrderResponse(request, order.id);

    const watcher = await browser.newPage();
    await watcher.goto('/');
    await watcher.waitForSelector('.table-picker');

    const watchedTableRow = watcher.locator('.floor-table', { has: watcher.getByTestId(`table-${table.label}`) });
    await expect(watchedTableRow).toHaveClass(/floor-table-Dirty/);

    await clearTable(request, table.id);

    // No watcher.reload() anywhere above this line.
    await expect(watchedTableRow).toHaveClass(/floor-table-Free/, { timeout: 10_000 });
    await watcher.close();
  });
});
