import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { openAnyFreeTable } from './support/ui';

// QA-14 — automated accessibility checks against the screens a real service
// shift actually uses: the table picker, the ordering screen, the modifier
// picker modal, and the receipt. Scoped to WCAG 2.0/2.1 A and AA rules (the
// axe defaults minus the "best-practice" tag, which is opinionated style
// rather than a real conformance requirement) so a failure here means an
// actual accessibility barrier, not a style-guide nit.

test.describe('accessibility', () => {
  test('table picker has no WCAG A/AA violations', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations, describeViolations(results.violations)).toEqual([]);
  });

  test('ordering screen, modifier picker, and receipt have no WCAG A/AA violations', async ({ page }) => {
    await page.goto('/');
    await openAnyFreeTable(page, 2);

    let results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations, describeViolations(results.violations)).toEqual([]);

    // The modifier picker is a modal dialog — the highest-risk surface for
    // focus and labelling issues, worth checking in isolation.
    await page.getByRole('button', { name: 'Frango na Brasa' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();

    results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations, describeViolations(results.violations)).toEqual([]);

    await page.getByTestId('modifier-Dose normal').click();
    await page.getByTestId('confirm-modifiers').click();
    await page.getByRole('button', { name: 'Imperial' }).click();

    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();

    results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations, describeViolations(results.violations)).toEqual([]);

    // Cleanup: back to the picker and return the table to the free pool.
    const tableLabel = await page
      .locator('.receipt-table')
      .textContent()
      .then((t) => t?.trim() ?? '');
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
