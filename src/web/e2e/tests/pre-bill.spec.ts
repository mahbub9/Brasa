import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  defaultRequiredModifierIds,
  findMenuItem,
  getMenu,
  getPreBill,
  getPreBillResponse,
  openOrderOnAnyFreeTable,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

// ORD-18/19 — the pre-bill a table sees before paying is a documento não
// fiscal, never an invoice: issuing it as one would fiscalise every table
// that merely asks to see the bill (docs/fiscal/README.md). These specs
// check that boundary is real, not just documented: the response shape has
// no document number/ATCUD/QR at all, requesting it repeatedly ("reprint")
// reproduces identical figures, and it is refused outside an open order —
// the same guard AddLine already uses (order.not_open, order.empty).

test.describe('pre-bill — a documento não fiscal, not an invoice', () => {
  test('reflects current lines and VAT breakdown, and a reprint matches exactly', async ({ request }) => {
    const menu = await getMenu(request);
    const frango = findMenuItem(menu, 'Frango na Brasa'); // 13% band
    const imperial = findMenuItem(menu, 'Imperial'); // 23% band (alcoholic)

    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    await addLine(request, order.id, frango.id, 1, defaultRequiredModifierIds(frango));
    const updated = await addLine(request, order.id, imperial.id, 1);

    const first = await getPreBill(request, order.id);

    // No fiscal artefact anywhere on the wire — a client that only ever saw
    // this response could not accidentally render it as a receipt.
    const raw = first as unknown as Record<string, unknown>;
    expect(raw.documentNumber).toBeUndefined();
    expect(raw.atcud).toBeUndefined();
    expect(raw.qrPayload).toBeUndefined();
    expect(first.documentKind).toBe('documento_nao_fiscal');

    expect(first.orderId).toBe(order.id);
    expect(first.tableLabel).toBe(table.label);
    expect(first.total.amount).toBeCloseTo(updated.total.amount, 2);
    expect(first.lines.map((l) => l.itemName).sort()).toEqual(['Frango na Brasa', 'Imperial'].sort());

    // Two VAT bands (13% and 23%), each band's net+VAT reconciling to its
    // own gross, and the bands summing back to the order total — the same
    // gross-inclusive derivation the real fiscal document will use
    // (FiscalDocumentLine), computed here without ever calling IFiscalProvider.
    expect(first.vatBreakdown).toHaveLength(2);
    const bandTotal = first.vatBreakdown.reduce((sum, band) => sum + band.grossTotal.amount, 0);
    expect(bandTotal).toBeCloseTo(first.total.amount, 2);
    for (const band of first.vatBreakdown) {
      expect(band.netTotal.amount + band.vatAmount.amount).toBeCloseTo(band.grossTotal.amount, 2);
    }

    // Reprint: fetching again must reproduce identical figures. generatedAtUtc
    // is the only field allowed to differ — it is never persisted or numbered,
    // so there is nothing else that could drift between two calls.
    const second = await getPreBill(request, order.id);
    expect(second.total).toEqual(first.total);
    expect(second.lines).toEqual(first.lines);
    expect(second.vatBreakdown).toEqual(first.vatBreakdown);
    expect(second.documentKind).toBe(first.documentKind);

    await closeOrderAndClearTable(request, order.id, table.id);
  });

  test('is refused for an empty order and for a closed order', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTable(request, 1);

    const emptyResponse = await getPreBillResponse(request, order.id);
    expect(emptyResponse.status()).toBe(400);
    expect((await emptyResponse.json()).code).toBe('order.empty');

    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    await addLine(request, order.id, imperial.id, 1);

    await closeOrderAndClearTable(request, order.id, table.id);

    const closedResponse = await getPreBillResponse(request, order.id);
    expect(closedResponse.status()).toBe(409);
    expect((await closedResponse.json()).code).toBe('order.not_open');
  });

  test('"Ver conta" shows the non-fiscal notice and passes WCAG A/AA as a dialog', async ({ page }) => {
    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    await page.getByRole('button', { name: 'Frango na Brasa' }).click();
    await page.getByTestId('modifier-Dose normal').click();
    await page.getByTestId('confirm-modifiers').click();

    await page.getByTestId('pre-bill-button').click();

    const dialog = page.getByRole('dialog', { name: 'Conta' });
    await expect(dialog).toBeVisible();
    await expect(page.getByTestId('pre-bill-notice')).toHaveText('Documento não fiscal — não serve de fatura');
    await expect(page.getByTestId('pre-bill-total')).toHaveText(/9,50/);

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations, describeViolations(results.violations)).toEqual([]);

    // Reprint from the UI too: closing and reopening the dialog must show
    // the same total, still with no fiscal fields anywhere on the page.
    await page.getByTestId('close-pre-bill').click();
    await expect(dialog).toBeHidden();
    await page.getByTestId('pre-bill-button').click();
    await expect(page.getByTestId('pre-bill-total')).toHaveText(/9,50/);
    await page.getByTestId('close-pre-bill').click();

    // Cleanup: close the real order and return the table to the free pool.
    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();
  });
});

function describeViolations(violations: { id: string; help: string; nodes: { target: string[] }[] }[]): string {
  if (violations.length === 0) return '';
  return violations
    .map((v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`)
    .join('\n');
}
