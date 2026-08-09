import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  findMenuItem,
  getFloor,
  getMenu,
  openOrderOnAnyFreeTable,
  transferOrderResponse,
  transferOrderToAnyFreeTable,
} from './support/api';
import { openAnyFreeTable, transferToAnyFreeTable } from './support/ui';

// ORD-12 — moving an open order to a different table mid-service. Floor and
// Ordering are separate modules/DbContexts (module-boundaries.md rule 5);
// this exercises the one composing endpoint where both table mutations
// commit atomically together in a single FloorDbContext.SaveChangesAsync,
// unlike the OpenOrderAsync/CloseOrderAsync pair which must coordinate two.

test.describe('table transfer', () => {
  test('moves the order to a new table, freeing the old one', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table: oldTable } = await openOrderOnAnyFreeTable(request, 2);
    await addLine(request, order.id, imperial.id, 1);

    const { order: transferred, table: newTable } = await transferOrderToAnyFreeTable(request, order.id);
    expect(newTable.id).not.toBe(oldTable.id);
    expect(transferred.tableId).toBe(newTable.id);
    expect(transferred.tableLabel).toBe(newTable.label);
    // The line rung up before the transfer must survive it untouched.
    expect(transferred.total.amount).toBeGreaterThan(0);

    const floorAfter = await getFloor(request);
    const oldTableAfter = floorAfter.flatMap((r) => r.tables).find((t) => t.id === oldTable.id);
    const newTableAfter = floorAfter.flatMap((r) => r.tables).find((t) => t.id === newTable.id);
    expect(oldTableAfter?.state).toBe('Free');
    expect(newTableAfter?.state).toBe('Occupied');

    await closeOrderAndClearTable(request, order.id, newTable.id);
  });

  test('rejects a table that is already occupied, an unknown table, and a closed order', async ({ request }) => {
    const { order: orderA, table: tableA } = await openOrderOnAnyFreeTable(request, 1);
    const { order: orderB, table: tableB } = await openOrderOnAnyFreeTable(request, 1);

    // Both checks below run while orderA is still Open, so each exercises
    // exactly the guard it's meant to (order.not_open is checked before the
    // target table is even looked up — see TransferOrderAsync's ordering).
    const ontoOccupied = await transferOrderResponse(request, orderA.id, tableB.id);
    expect(ontoOccupied.status()).toBe(409);
    expect((await ontoOccupied.json()).code).toBe('floor.table_not_free');

    const unknownTable = await transferOrderResponse(request, orderA.id, crypto.randomUUID());
    expect(unknownTable.status()).toBe(404);
    expect((await unknownTable.json()).code).toBe('floor.table_not_found');

    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    await addLine(request, orderA.id, imperial.id, 1);
    await closeOrderAndClearTable(request, orderA.id, tableA.id);

    const onClosedOrder = await transferOrderResponse(request, orderA.id, tableB.id);
    expect(onClosedOrder.status()).toBe(409);
    expect((await onClosedOrder.json()).code).toBe('order.not_open');

    await addLine(request, orderB.id, imperial.id, 1);
    await closeOrderAndClearTable(request, orderB.id, tableB.id);
  });

  test('"Transferir mesa" moves the order to a picked free table through the UI', async ({ page }) => {
    // This test chains two independently-retrying helpers — openAnyFreeTable
    // for the initial seating, then transferToAnyFreeTable for the move —
    // each good for up to 5 attempts × 5s under real contention for the
    // shared 16-table pool (QA-02). Their worst cases can add, so the default
    // 30s (or even 60s) test timeout is too tight; this gives real headroom.
    test.setTimeout(120_000);

    await page.goto('/');
    const originalLabel = await openAnyFreeTable(page, 2);
    await expect(page.getByRole('heading', { name: originalLabel })).toBeVisible();

    await page.getByTestId('transfer-table-button').click();
    const dialog = page.getByRole('dialog', { name: 'Transferir para' });
    await expect(dialog).toBeVisible();

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);

    const targetLabel = await transferToAnyFreeTable(page);
    await expect(page.getByRole('heading', { name: targetLabel })).toBeVisible();

    // Cleanup: close and clear the table the order now lives on.
    await page.getByRole('button', { name: 'Imperial' }).click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${targetLabel}`).click();
  });
});
