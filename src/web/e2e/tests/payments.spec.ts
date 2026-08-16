import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrder,
  clearTable,
  findMenuItem,
  getMenu,
  getPayments,
  getPaymentsResponse,
  openOrderOnAnyFreeTable,
  recordPayment,
  recordPaymentResponse,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

// PAY-01/02/05 — a cash tender recorded against an order's remaining
// balance, with change calculated server-side. Deliberately does NOT gate
// Order.Close() — see Payment.cs's own remarks — so these tests record
// payments both before and after close to prove neither is a precondition
// the endpoint secretly enforces. A tender smaller than what's owed is a
// valid partial payment (PAY-05): the order's own balance tracks across
// however many Payment rows it takes to reach zero, and a further payment
// against an already-settled order is rejected.

test.describe('cash payments (PAY-01/02/05)', () => {
  test('records a cash tender covering the total and computes change', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 2); // 2 x 2.50 = 5.00

    const payment = await recordPayment(request, order.id, 'Cash', 10);

    expect(payment.orderId).toBe(order.id);
    expect(payment.method).toBe('Cash');
    expect(payment.amountDue).toEqual({ amount: 5, currency: 'EUR' });
    expect(payment.amountTendered).toEqual({ amount: 10, currency: 'EUR' });
    expect(payment.amountApplied).toEqual({ amount: 5, currency: 'EUR' });
    expect(payment.change).toEqual({ amount: 5, currency: 'EUR' });
    expect(payment.remainingBalance).toEqual({ amount: 0, currency: 'EUR' });

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('a tender smaller than the total is a valid partial payment, tracked to a zero balance', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 2); // 5.00 due

    const first = await recordPayment(request, order.id, 'Cash', 3);
    expect(first.amountDue).toEqual({ amount: 5, currency: 'EUR' });
    expect(first.amountTendered).toEqual({ amount: 3, currency: 'EUR' });
    expect(first.amountApplied).toEqual({ amount: 3, currency: 'EUR' });
    expect(first.change).toEqual({ amount: 0, currency: 'EUR' });
    expect(first.remainingBalance).toEqual({ amount: 2, currency: 'EUR' });

    // The settling tender overpays the remaining 2.00 — proves change and
    // partial-payment tracking compose correctly, not just each in isolation.
    const second = await recordPayment(request, order.id, 'Cash', 3);
    expect(second.amountDue).toEqual({ amount: 2, currency: 'EUR' });
    expect(second.amountTendered).toEqual({ amount: 3, currency: 'EUR' });
    expect(second.amountApplied).toEqual({ amount: 2, currency: 'EUR' });
    expect(second.change).toEqual({ amount: 1, currency: 'EUR' });
    expect(second.remainingBalance).toEqual({ amount: 0, currency: 'EUR' });

    const third = await recordPaymentResponse(request, order.id, 'Cash', 1);
    expect(third.status()).toBe(400);
    expect((await third.json()).code).toBe('payment.already_settled');

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('rejects zero and negative amounts, an unsupported method, and an unknown order', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 1);

    const zero = await recordPaymentResponse(request, order.id, 'Cash', 0);
    expect(zero.status()).toBe(400);
    expect((await zero.json()).code).toBe('payment.invalid_amount_tendered');

    const negative = await recordPaymentResponse(request, order.id, 'Cash', -5);
    expect(negative.status()).toBe(400);
    expect((await negative.json()).code).toBe('payment.invalid_amount_tendered');

    const unsupported = await recordPaymentResponse(request, order.id, 'Card', 100);
    expect(unsupported.status()).toBe(400);
    expect((await unsupported.json()).code).toBe('payment.unsupported_method');

    const unknownOrder = await recordPaymentResponse(request, '00000000-0000-0000-0000-000000000000', 'Cash', 100);
    expect(unknownOrder.status()).toBe(404);
    expect((await unknownOrder.json()).code).toBe('order.not_found');

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('lists every payment recorded against an order, and 404s for an unknown one', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 1); // 2.50 due

    expect(await getPayments(request, order.id)).toEqual([]);

    await recordPayment(request, order.id, 'Cash', 1); // partial
    await recordPayment(request, order.id, 'Cash', 5); // settles + change
    const payments = await getPayments(request, order.id);
    expect(payments).toHaveLength(2);
    expect(payments[0].remainingBalance).toEqual({ amount: 1.5, currency: 'EUR' });
    expect(payments[1].change).toEqual({ amount: 3.5, currency: 'EUR' });
    expect(payments[1].remainingBalance).toEqual({ amount: 0, currency: 'EUR' });

    const unknown = await getPaymentsResponse(request, '00000000-0000-0000-0000-000000000000');
    expect(unknown.status()).toBe(404);

    await closeOrder(request, order.id);
    await clearTable(request, table.id);
  });

  test('recording a payment after close still works — Close() is not a precondition', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 1);
    await closeOrder(request, order.id);

    const payment = await recordPayment(request, order.id, 'Cash', 3);
    expect(payment.orderId).toBe(order.id);

    await clearTable(request, table.id);
  });

  test('the pos UI records a cash payment on the receipt screen and shows the change due', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();

    const tableLabel = await openAnyFreeTable(page, 2);

    const bread = page.getByRole('button', { name: 'Pão e Azeitonas' });
    await expect(bread).toBeVisible();
    await bread.click();
    await bread.click();
    await expect(page.getByTestId('order-total')).toHaveText(/5,00/);

    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();

    await expect(page.getByText('Valor a pagar: 5,00')).toBeVisible();
    await page.getByTestId('cash-payment-tendered').fill('10');
    await page.getByTestId('cash-payment-submit').click();

    await expect(page.getByTestId('cash-payment-done')).toBeVisible();
    await expect(page.getByTestId('cash-payment-change')).toHaveText(/5,00/);

    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();
    await page.getByTestId(`table-${tableLabel}`).click();
  });

  test('the pos UI records a partial tender, shows the remaining balance, then settles on a second tender', async ({
    page,
  }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();

    const tableLabel = await openAnyFreeTable(page, 2);

    const bread = page.getByRole('button', { name: 'Pão e Azeitonas' });
    await expect(bread).toBeVisible();
    await bread.click();
    await bread.click();
    await expect(page.getByTestId('order-total')).toHaveText(/5,00/);

    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();

    await expect(page.getByText('Valor a pagar: 5,00')).toBeVisible();
    await page.getByTestId('cash-payment-tendered').fill('3');
    await page.getByTestId('cash-payment-submit').click();

    // Still unsettled — the form stays open showing the remaining balance,
    // not the "done" screen, and lists the partial tender already recorded.
    await expect(page.getByTestId('cash-payment-done')).not.toBeVisible();
    await expect(page.getByTestId('cash-payment-remaining')).toHaveText(/2,00/);
    await expect(page.getByTestId('cash-payment-history')).toBeVisible();

    await page.getByTestId('cash-payment-tendered').fill('2');
    await page.getByTestId('cash-payment-submit').click();

    await expect(page.getByTestId('cash-payment-done')).toBeVisible();
    await expect(page.getByTestId('cash-payment-change')).toHaveText(/0,00/);

    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();
    await page.getByTestId(`table-${tableLabel}`).click();
  });
});
