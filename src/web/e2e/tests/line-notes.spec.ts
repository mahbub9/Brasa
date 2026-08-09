import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  findMenuItem,
  getMenu,
  openOrderOnAnyFreeTable,
  setLineNotes,
  setLineNotesResponse,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

// ORD-06 — free-text kitchen notes on an order line, added after the line is
// already rung up (editing a line itself isn't built yet — ORD-03). Covers
// both the API contract directly and the inline editor in pos.

test.describe('kitchen notes on an order line', () => {
  test('sets, overwrites and clears a note via the API', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const afterSet = await setLineNotes(request, order.id, lineId, 'sem gelo');
    expect(afterSet.lines[0].notes).toBe('sem gelo');

    const afterOverwrite = await setLineNotes(request, order.id, lineId, 'com gelo, por favor');
    expect(afterOverwrite.lines[0].notes).toBe('com gelo, por favor');

    const afterClear = await setLineNotes(request, order.id, lineId, null);
    expect(afterClear.lines[0].notes).toBeNull();

    await closeOrderAndClearTable(request, order.id, table.id);
  });

  test('rejects an unknown line, a too-long note, and a closed order', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);
    const withLine = await addLine(request, order.id, imperial.id, 1);
    const lineId = withLine.lines[0].id;

    const unknownLine = await setLineNotesResponse(request, order.id, crypto.randomUUID(), 'x');
    expect(unknownLine.status()).toBe(404);
    expect((await unknownLine.json()).code).toBe('order.line_not_found');

    const tooLong = await setLineNotesResponse(request, order.id, lineId, 'x'.repeat(301));
    expect(tooLong.status()).toBe(400);
    expect((await tooLong.json()).code).toBe('order.notes_too_long');

    await closeOrderAndClearTable(request, order.id, table.id);

    const onClosedOrder = await setLineNotesResponse(request, order.id, lineId, 'too late');
    expect(onClosedOrder.status()).toBe(409);
    expect((await onClosedOrder.json()).code).toBe('order.not_open');
  });

  test('adding, editing and clearing a note through the inline editor', async ({ page }) => {
    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    await page.getByRole('button', { name: 'Imperial' }).click();
    await expect(page.getByText('Ainda sem itens')).toBeHidden();

    await page.locator('.order-line-notes-add').first().click();
    const input = page.locator('.order-line-notes-edit input');
    await input.fill('sem gelo');
    await page.getByRole('button', { name: 'Guardar' }).click();

    const display = page.locator('.order-line-notes-display').first();
    await expect(display).toHaveText('Nota: sem gelo');

    // Editing again must start from the saved value, not a stale draft.
    await display.click();
    await expect(page.locator('.order-line-notes-edit input')).toHaveValue('sem gelo');
    await page.locator('.order-line-notes-edit input').fill('');
    await page.getByRole('button', { name: 'Guardar' }).click();

    // An empty save clears the note back to the "add a note" affordance.
    await expect(page.locator('.order-line-notes-add')).toBeVisible();
    await expect(page.locator('.order-line-notes-display')).toHaveCount(0);

    // Cleanup: close and clear so the table returns to the free pool.
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
  });
});
