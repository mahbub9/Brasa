import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  defaultRequiredModifierIds,
  findMenuItem,
  getMenu,
  openOrderOnAnyFreeTable,
} from './support/api';

// QA-03 — exercises the deterministic test-data builders directly against
// the API (Playwright's `request` fixture, no browser). Complements
// walking-skeleton.spec.ts: that spec proves the UI renders a split
// correctly for one case; this one is cheap enough to sweep several part
// counts and confirm Money.Allocate's invariant — shares always sum back to
// the exact original total, to the cent — holds beyond the one number the
// UI test happens to use.

test.describe('split preview — Money.Allocate invariant', () => {
  for (const parts of [1, 2, 3, 5, 7]) {
    test(`splitting into ${parts} share(s) always sums back to the total`, async ({ request }) => {
      const menu = findMenuItemFixtures(await getMenu(request));
      const { order, table } = await openOrderOnAnyFreeTable(request, 2);
      // Frango na Brasa carries a required "Tamanho" group (CAT-03/04) —
      // the default selection is zero-price, so the well-known 22.60 total
      // below is unaffected.
      await addLine(request, order.id, menu.frango.id, 2, defaultRequiredModifierIds(menu.frango));
      const updated = await addLine(request, order.id, menu.imperial.id, 2);

      expect(updated.total.amount).toBeCloseTo(22.6, 2);

      const response = await request.get(
        `${apiUrl()}/orders/${order.id}/split?parts=${parts}`,
      );
      expect(response.ok()).toBe(true);
      const shares: { amount: number; currency: string }[] = await response.json();

      expect(shares).toHaveLength(parts);
      const sum = shares.reduce((total, share) => total + share.amount, 0);
      expect(sum).toBeCloseTo(updated.total.amount, 2);

      // Allocate hands any remainder to the earliest shares, never dropping
      // or inventing a cent — no share can differ from another by more than
      // one cent.
      const amounts = shares.map((s) => s.amount);
      expect(Math.max(...amounts) - Math.min(...amounts)).toBeLessThanOrEqual(0.01 + 1e-9);

      // Only 8 tables exist in the seeded floor plan and the dev database
      // isn't reset between runs — give this one back or repeated runs
      // eventually run out. See the comment in support/api.ts.
      await closeOrderAndClearTable(request, order.id, table.id);
    });
  }
});

function apiUrl(): string {
  return (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';
}

function findMenuItemFixtures(categories: Awaited<ReturnType<typeof getMenu>>) {
  return {
    frango: findMenuItem(categories, 'Frango na Brasa'),
    imperial: findMenuItem(categories, 'Imperial'),
  };
}
