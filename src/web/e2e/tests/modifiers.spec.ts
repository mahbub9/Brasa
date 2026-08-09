import { expect, test } from '@playwright/test';
import { openAnyFreeTable } from './support/ui';

// CAT-03/04 — the modifier picker itself: a required single-select group
// blocks "Adicionar" until satisfied, a multi-select group's price deltas
// add up correctly on the rung-up line, and Cancel adds nothing at all.
// Complements walking-skeleton.spec.ts, which only ever picks the zero-price
// default so its regression numbers stay unchanged.

test('modifier picker enforces the required group and totals selected price deltas', async ({ page }) => {
  await page.goto('/');

  const tableLabel = await openAnyFreeTable(page, 2);

  const frango = page.getByRole('button', { name: 'Frango na Brasa' });
  await frango.click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Required "Tamanho" not yet chosen — Adicionar must stay disabled.
  const addButton = page.getByTestId('confirm-modifiers');
  await expect(addButton).toBeDisabled();

  // Cancel with nothing confirmed must add nothing to the order.
  await page.getByRole('button', { name: 'Cancelar' }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText('Ainda sem itens')).toBeVisible();

  // Re-open and actually complete a selection this time: base size plus two
  // extras (+1,00 and +1,50) on top of the 9,50 base = 12,00.
  await frango.click();
  await expect(addButton).toBeDisabled();

  await page.getByTestId('modifier-Meia dose').click(); // -2.50, single-select
  await expect(addButton).toBeEnabled();

  await page.getByTestId('modifier-Extra queijo').click();
  await page.getByTestId('modifier-Sem piri-piri').click(); // zero-price, still a real selection
  await addButton.click();
  await expect(dialog).toBeHidden();

  // 9.50 - 2.50 (Meia dose) + 1.00 (Extra queijo) + 0.00 (Sem piri-piri) = 8.00
  await expect(page.getByTestId('order-total')).toHaveText(/8,00/);

  const modifiersShown = page.locator('.order-line-modifiers li');
  await expect(modifiersShown).toHaveCount(3);
  await expect(modifiersShown.getByText('Meia dose')).toBeVisible();
  await expect(modifiersShown.getByText('Extra queijo')).toBeVisible();

  // Cleanup: close and clear so the table returns to the free pool.
  await page.getByTestId('close-order-button').click();
  await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
  await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
  await page.getByTestId(`table-${tableLabel}`).click();
});
