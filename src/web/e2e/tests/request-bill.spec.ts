import { expect, test } from '@playwright/test';
import { getFloor, requestBillResponse } from './support/api';
import { openAnyFreeTable } from './support/ui';

// FLR-04 — Table.RequestBill() already existed as a domain transition
// (State: Free ⇄ Occupied ⇄ Dirty ⇄ Free, plus BillRequested) with no
// endpoint or UI; this closes that gap. Distinct from the pre-bill
// (ORD-18/19, "Ver conta" — a read-only preview of the total): "Pedir
// conta" is a floor-plan signal for staff that guests have asked to pay,
// so a GET can stay side-effect-free and this stays an explicit action.

test.describe('request bill (floor-plan signal)', () => {
  test('"Pedir conta" flags the table BillRequested, visible on GET /floor', async ({ page, request }) => {
    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    await page.getByRole('button', { name: 'Água' }).click();
    await page.getByTestId('modifier-Com gás').click();
    await page.getByTestId('confirm-modifiers').click();

    await page.getByTestId('request-bill-button').click();

    await expect
      .poll(async () => {
        const rooms = await getFloor(request);
        const table = rooms.flatMap((r) => r.tables).find((t) => t.label === tableLabel);
        return table?.state;
      })
      .toBe('BillRequested');

    // Cleanup: closing works from BillRequested too (MarkDirty already
    // accepts Occupied or BillRequested — see Table.cs) and returns the
    // table to the free pool the same way every other spec does.
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
  });

  test('rejects requesting a bill for a free table and an unknown table', async ({ request }) => {
    const rooms = await getFloor(request);
    const freeTable = rooms.flatMap((r) => r.tables).find((t) => t.state === 'Free');
    if (!freeTable) {
      throw new Error('No free table available — the seeded floor plan may be exhausted.');
    }

    const freeResponse = await requestBillResponse(request, freeTable.id);
    expect(freeResponse.status()).toBe(409);
    expect((await freeResponse.json()).code).toBe('floor.table_not_occupied');

    const unknownResponse = await requestBillResponse(request, crypto.randomUUID());
    expect(unknownResponse.status()).toBe(404);
    expect((await unknownResponse.json()).code).toBe('floor.table_not_found');
  });
});
