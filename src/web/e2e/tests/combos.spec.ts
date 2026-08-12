import { expect, test } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';
import {
  addComboComponent,
  addComboComponentResponse,
  addComboLine,
  addComboLineResponse,
  addLine,
  closeOrderAndClearTable,
  createCombo,
  createComboResponse,
  deleteMenuItem,
  findMenuItem,
  getCombo,
  getComboResponse,
  getMenu,
  getPreBill,
  importMenuItemsResponse,
  openOrderOnAnyFreeTable,
  updateMenuItemAvailabilityResponse,
} from './support/api';

// CAT-10 — a combo (menu do dia) is never a new fiscal concept: ringing one
// up (POST /orders/{id}/combo-lines) allocates its fixed price across
// components via Money.Allocate, weighted by each component's own current
// standalone price — the same proration ORD-11 already uses for an
// order-level discount — then adds each component as an ordinary order
// line at its own real VAT rate. This suite proves that decomposition is
// correct, not just that the endpoints exist: two components at different
// VAT bands (13%/23%) must itemize separately and reconcile to the cent.
//
// Uses freshly-imported items, isolated from the shared seeded menu other
// specs read concurrently — same reasoning as menu-item-takeaway-price.spec.ts.

async function createTestItem(request: APIRequestContext, price: string, vatRate: string): Promise<{ id: string; name: string }> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const name = `Combo Test ${crypto.randomUUID()}`;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},${price},${vatRate}\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
  return { id: item.id, name };
}

test.describe('combos (CAT-10)', () => {
  test('rings a combo up as separate lines, allocated proportionally, each at its own VAT rate, reconciling to the cent', async ({
    request,
  }) => {
    // Deliberately different VAT bands (13%/23%) and different standalone
    // prices (4.00/3.00 -> 4:3 weight ratio) so the proration is provably
    // not an even split, and the fiscal breakdown must show two bands.
    const soup = await createTestItem(request, '4.00', '0.13');
    const wine = await createTestItem(request, '3.00', '0.23');

    try {
      const combo = await createCombo(request, `Combo Test ${crypto.randomUUID()}`, 6.0);
      await addComboComponent(request, combo.id, soup.id);
      await addComboComponent(request, combo.id, wine.id);

      const { order, table } = await openOrderOnAnyFreeTable(request, 2);
      try {
        const updated = await addComboLine(request, order.id, combo.id);
        expect(updated.lines.length).toBe(2);

        const soupLine = updated.lines.find((l) => l.itemName === soup.name);
        const wineLine = updated.lines.find((l) => l.itemName === wine.name);
        expect(soupLine).toBeDefined();
        expect(wineLine).toBeDefined();

        // 6.00 allocated by weight [400, 300] (minor units) -> 343/257,
        // Money.Allocate's own deterministic remainder rule (§ its own
        // doc comment) puts the leftover cent on the first weight.
        expect(soupLine!.unitPrice.amount).toBeCloseTo(3.43, 2);
        expect(wineLine!.unitPrice.amount).toBeCloseTo(2.57, 2);
        expect(soupLine!.lineTotal.amount + wineLine!.lineTotal.amount).toBeCloseTo(6.0, 2);
        expect(updated.total.amount).toBeCloseTo(6.0, 2);

        // The fiscal breakdown must show both bands separately -- a combo
        // mixing rates can never collapse into one figure -- and still
        // reconcile exactly to the combo's own fixed price.
        const preBill = await getPreBill(request, order.id);
        expect(preBill.vatBreakdown.length).toBe(2);
        const totalAcrossBands = preBill.vatBreakdown.reduce((sum, band) => sum + band.grossTotal.amount, 0);
        expect(totalAcrossBands).toBeCloseTo(6.0, 2);
      } finally {
        await closeOrderAndClearTable(request, order.id, table.id);
      }
    } finally {
      await deleteMenuItem(request, soup.id);
      await deleteMenuItem(request, wine.id);
    }
  });

  test('rejects an empty combo name and a negative price', async ({ request }) => {
    const emptyName = await createComboResponse(request, '   ', 5.0);
    expect(emptyName.status()).toBe(400);
    expect((await emptyName.json()).code).toBe('catalog.invalid_combo_name');

    const negative = await createComboResponse(request, `Combo Test ${crypto.randomUUID()}`, -1);
    expect(negative.status()).toBe(400);
    expect((await negative.json()).code).toBe('catalog.invalid_price');
  });

  test('rejects a duplicate component, an unknown item, and an unknown combo', async ({ request }) => {
    const item = await createTestItem(request, '2.00', '0.13');
    try {
      const combo = await createCombo(request, `Combo Test ${crypto.randomUUID()}`, 2.0);
      await addComboComponent(request, combo.id, item.id);

      const duplicate = await addComboComponentResponse(request, combo.id, item.id);
      expect(duplicate.status()).toBe(409);
      expect((await duplicate.json()).code).toBe('catalog.combo_component_exists');

      const unknownItem = await addComboComponentResponse(request, combo.id, crypto.randomUUID());
      expect(unknownItem.status()).toBe(404);
      expect((await unknownItem.json()).code).toBe('catalog.item_not_found');

      const unknownCombo = await addComboComponentResponse(request, crypto.randomUUID(), item.id);
      expect(unknownCombo.status()).toBe(404);
      expect((await unknownCombo.json()).code).toBe('catalog.combo_not_found');
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });

  test('rejects ringing up an empty combo, an unavailable component, and an unknown combo', async ({ request }) => {
    // Every sub-scenario below rejects the combo-line call, leaving its
    // order with zero lines — closeOrder refuses to close one of those
    // (order.empty), so each gets one real line first before closing, the
    // same recovery void-line.spec.ts already establishes for a rejected
    // close. "Imperial" has no required modifiers, so a plain quantity-1
    // add always succeeds.
    const imperial = findMenuItem(await getMenu(request), 'Imperial');

    const emptyCombo = await createCombo(request, `Combo Test ${crypto.randomUUID()}`, 5.0);
    const { order: order1, table: table1 } = await openOrderOnAnyFreeTable(request, 2);
    try {
      const emptyResponse = await addComboLineResponse(request, order1.id, emptyCombo.id);
      expect(emptyResponse.status()).toBe(400);
      expect((await emptyResponse.json()).code).toBe('catalog.combo_has_no_components');

      await addLine(request, order1.id, imperial.id, 1);
    } finally {
      await closeOrderAndClearTable(request, order1.id, table1.id);
    }

    const item = await createTestItem(request, '2.00', '0.13');
    try {
      const markUnavailable = await updateMenuItemAvailabilityResponse(request, item.id, false);
      expect(markUnavailable.ok()).toBe(true);
      const combo = await createCombo(request, `Combo Test ${crypto.randomUUID()}`, 2.0);
      await addComboComponent(request, combo.id, item.id);

      const { order: order2, table: table2 } = await openOrderOnAnyFreeTable(request, 2);
      try {
        const unavailable = await addComboLineResponse(request, order2.id, combo.id);
        expect(unavailable.status()).toBe(409);
        expect((await unavailable.json()).code).toBe('catalog.item_unavailable');

        const unknownCombo = await addComboLineResponse(request, order2.id, crypto.randomUUID());
        expect(unknownCombo.status()).toBe(404);
        expect((await unknownCombo.json()).code).toBe('catalog.combo_not_found');

        // Same recovery as order1 above -- both combo-line attempts were
        // rejected, so order2 still has zero lines.
        await addLine(request, order2.id, imperial.id, 1);
      } finally {
        await closeOrderAndClearTable(request, order2.id, table2.id);
      }
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });

  test('404s for an unknown combo on GET', async ({ request }) => {
    const response = await getComboResponse(request, crypto.randomUUID());
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('catalog.combo_not_found');
  });

  test('persists components across a refetch', async ({ request }) => {
    const item = await createTestItem(request, '1.50', '0.13');
    try {
      const combo = await createCombo(request, `Combo Test ${crypto.randomUUID()}`, 1.5);
      await addComboComponent(request, combo.id, item.id);

      const refetched = await getCombo(request, combo.id);
      expect(refetched.components).toEqual([{ id: refetched.components[0].id, menuItemId: item.id }]);
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });
});
