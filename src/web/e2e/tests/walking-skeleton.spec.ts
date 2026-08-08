import { expect, test } from '@playwright/test';

// QA-05 — the I0 smoke test everything else builds on (see
// docs/development/e2e-testing.md). Drives the real rendered pos UI in a
// real browser: open a table, add a mixed food+alcohol order, preview a
// split, close, and check the receipt. This is deliberately the one spec
// that does NOT use the API builders in support/api.ts — it exists to prove
// the UI itself works, not just the API behind it.
//
// Mirrors the numbers from the backend's own live verification
// (docs/product/status.md#i0-demo) so a regression here is directly
// comparable to that record: 2x Frango na Brasa (13% VAT) + 2x Imperial
// (23% VAT) = 22.60 EUR, split 3 ways = 7.54 / 7.53 / 7.53.

test('open a table, ring up a mixed order, split it, close it, get a receipt', async ({ page }) => {
  const tableLabel = `E2E Mesa ${Date.now()}`;

  await page.goto('/');

  await page.getByLabel('Table').fill(tableLabel);
  await page.getByLabel('Covers').fill('3');
  await page.getByRole('button', { name: 'Open table' }).click();

  await expect(page.getByRole('heading', { name: tableLabel })).toBeVisible();

  const frango = page.getByRole('button', { name: 'Frango na Brasa' });
  const imperial = page.getByRole('button', { name: 'Imperial' });
  await expect(frango).toBeVisible();
  await expect(imperial).toBeVisible();

  await frango.click();
  await frango.click();
  await imperial.click();
  await imperial.click();

  await expect(page.getByTestId('order-total')).toHaveText(/22,60/);

  await page.getByTestId('split-parts-input').fill('3');
  await page.getByTestId('preview-split-button').click();

  const splitAmounts = page.getByTestId('split-amounts').locator('li');
  await expect(splitAmounts).toHaveCount(3);
  await expect(splitAmounts.nth(0)).toHaveText(/7,54/);
  await expect(splitAmounts.nth(1)).toHaveText(/7,53/);
  await expect(splitAmounts.nth(2)).toHaveText(/7,53/);

  // Captured before closing, so the receipt's gross total can be checked
  // against exactly what was shown during service — not just against a
  // hardcoded number. This is what would have caught the VAT-computed-
  // backwards bug from the browser side (docs/product/status.md#i0-demo).
  const orderTotalDuringService = await page.getByTestId('order-total').textContent();

  await page.getByTestId('close-order-button').click();

  await expect(page.getByRole('heading', { name: 'Receipt issued' })).toBeVisible();
  await expect(page.getByTestId('receipt-document-number')).toHaveText(/^FS /);
  await expect(page.getByTestId('receipt-atcud')).toHaveText(/\S/);
  await expect(page.getByTestId('receipt-net')).toHaveText(/19,74/);
  await expect(page.getByTestId('receipt-vat')).toHaveText(/2,86/);
  await expect(page.getByTestId('receipt-gross')).toHaveText(/22,60/);
  expect(await page.getByTestId('receipt-gross').textContent()).toBe(orderTotalDuringService);

  await page.getByRole('button', { name: 'Open another table' }).click();
  await expect(page.getByRole('heading', { name: 'Abrir mesa' })).toBeVisible();
});
