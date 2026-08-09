import { expect, test } from '@playwright/test';
import { addLine, closeOrder, findMenuItem, getMenu, getOrder, openTakeawayOrder } from './support/api';

// ORD-20 — a takeaway/counter-sale order, rung up with no Floor table
// involved at all. TableId stays the all-zero GUID and IsTakeaway is the
// real signal (see Order.OpenTakeaway's remarks) — never a magic check
// against Guid.Empty anywhere outside the domain itself.

test.describe('takeaway order', () => {
  test('opens with no table, closes normally, and issues a fiscal document', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');

    const order = await openTakeawayOrder(request, 'Ana');
    expect(order.isTakeaway).toBe(true);
    expect(order.tableLabel).toBe('Ana');
    expect(order.tableId).toBe('00000000-0000-0000-0000-000000000000');
    expect(order.coverCount).toBe(1);
    expect(order.status).toBe('Open');

    await addLine(request, order.id, imperial.id, 1);

    // No Floor table to clear — closeOrder alone is the full cleanup.
    const closed = await closeOrder(request, order.id);
    expect(closed.order.status).toBe('Closed');
    expect(closed.document.documentNumber).toBeTruthy();
    expect(closed.document.grossTotal.amount).toBeCloseTo(imperial.price.amount, 2);

    const refetched = await getOrder(request, order.id);
    expect(refetched.status).toBe('Closed');
  });

  test('defaults the label to "Levantamento" when none is given', async ({ request }) => {
    const order = await openTakeawayOrder(request, null);
    expect(order.tableLabel).toBe('Levantamento');
    expect(order.isTakeaway).toBe(true);

    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    await addLine(request, order.id, imperial.id, 1);
    await closeOrder(request, order.id);
  });

  test('"Nova venda ao balcão" opens a labelled order with no cover count shown', async ({ page }) => {
    await page.goto('/');

    await page.getByTestId('new-takeaway-button').click();
    await page.getByTestId('takeaway-label-input').fill('Take 42');
    await page.getByTestId('confirm-open-takeaway').click();

    await expect(page.getByRole('heading', { name: 'Take 42' })).toBeVisible();
    // Covers are meaningless for a counter sale — the line must not render.
    await expect(page.locator('.covers')).toHaveCount(0);

    await page.getByRole('button', { name: 'Imperial' }).click();
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();

    // Cleanup: back to the picker. No table to clear — nothing else to do.
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();
  });
});
