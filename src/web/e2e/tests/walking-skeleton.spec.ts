import { expect, test } from '@playwright/test';

// QA-05 — the I0 smoke test everything else builds on (see
// docs/development/e2e-testing.md). Drives the real rendered pos UI in a
// real browser: pick a free table off the floor plan, add a mixed
// food+alcohol order, preview a split, close, and check the receipt. This is
// deliberately the one spec that does NOT use the API builders in
// support/api.ts — it exists to prove the UI itself works, not just the API
// behind it.
//
// Runs in the app's default language (Portuguese, docs/architecture/
// decisions/0011-i18n.md) deliberately — that is what a real restaurant
// sees, and language-toggle.spec.ts covers the English path plus the
// toggle itself.
//
// Mirrors the numbers from the backend's own live verification
// (docs/product/status.md#i0-demo) so a regression here is directly
// comparable to that record: 2x Frango na Brasa (13% VAT) + 2x Imperial
// (23% VAT) = 22.60 EUR, split 3 ways = 7.54 / 7.53 / 7.53.

test('pick a table, ring up a mixed order, split it, close it, get a receipt', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();

  // Never hardcode a specific table — the seeded floor plan is shared with
  // other specs running in parallel. Any free (non-disabled) table works.
  const freeTable = page.locator('.floor-tables button:not([disabled])').first();
  const tableLabel = await freeTable.locator('.floor-table-label').textContent();
  await freeTable.click();

  await page.getByTestId('table-confirm').getByRole('spinbutton').fill('3');
  await page.getByTestId('confirm-open-table').click();

  await expect(page.getByRole('heading', { name: tableLabel ?? '' })).toBeVisible();

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

  await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
  await expect(page.getByTestId('receipt-document-number')).toHaveText(/^FS /);
  await expect(page.getByTestId('receipt-atcud')).toHaveText(/\S/);
  await expect(page.getByTestId('receipt-net')).toHaveText(/19,74/);
  await expect(page.getByTestId('receipt-vat')).toHaveText(/2,86/);
  await expect(page.getByTestId('receipt-gross')).toHaveText(/22,60/);
  expect(await page.getByTestId('receipt-gross').textContent()).toBe(orderTotalDuringService);

  await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
  await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();

  // Give the table back — see the file-level comment in support/api.ts on
  // why an E2E table must always return to the free pool.
  await page.getByTestId(`table-${tableLabel}`).click();
});
