import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  findMenuItem,
  getMenu,
  getOrder,
  openOrderOnAnyFreeTable,
  previewSplitByItem,
  previewSplitByItemResponse,
  setLineDiscount,
  voidLine,
} from './support/api';

// ORD-16 — splitting a bill by item rather than evenly: each guest pays for
// specific items. A pure preview (like the even split, GET /orders/{id}/split)
// — it never mutates order state, it just needs a structured body to say who
// gets what, so unlike the even split it can't stay a GET.

test.describe('split by item', () => {
  test('splits a partial line across two groups, exactly, with no rounding needed', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const frango = findMenuItem(menu, 'Frango na Brasa');
    const defaultModifierIds = frango.modifierGroups
      .filter((g) => g.isRequired)
      .map((g) => g.modifiers[0]?.id)
      .filter((id): id is string => id !== undefined);

    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const afterImperial = await addLine(request, order.id, imperial.id, 2); // 2 Imperiais, one line
    const withFrango = await addLine(request, order.id, frango.id, 1, defaultModifierIds);

    const imperialLine = afterImperial.lines.find((l) => l.itemName === 'Imperial')!;
    const frangoLine = withFrango.lines.find((l) => l.itemName === 'Frango na Brasa')!;

    // Guest A: 1 of the 2 Imperiais. Guest B: the other Imperial, plus the Frango.
    const result = await previewSplitByItem(request, order.id, [
      { lines: [{ lineId: imperialLine.id, quantity: 1 }] },
      { lines: [{ lineId: imperialLine.id, quantity: 1 }, { lineId: frangoLine.id, quantity: 1 }] },
    ]);

    expect(result.groups).toHaveLength(2);
    const [groupA, groupB] = result.groups;
    const sumOfGroups = groupA.total.amount + groupB.total.amount;
    expect(sumOfGroups).toBeCloseTo(withFrango.total.amount, 2);
    // Group A is exactly 1 Imperial (no modifiers) — its price is the menu's
    // unit price, with no rounding involved since this is one whole unit.
    expect(groupA.total.amount).toBeCloseTo(imperial.price.amount, 2);
    expect(groupA.lines).toHaveLength(1);
    expect(groupB.lines).toHaveLength(2);

    // Pure preview — the order itself must be completely unchanged.
    const orderAfter = await getOrder(request, order.id);
    expect(orderAfter.status).toBe('Open');
    expect(orderAfter.lines).toHaveLength(2);
    expect(orderAfter.total.amount).toBeCloseTo(withFrango.total.amount, 2);

    await closeOrderAndClearTable(request, order.id, table.id);
  });

  test('a discounted line splits at its discounted value, a voided line splits at zero (ORD-11/ORD-10)', async ({
    request,
  }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const frango = findMenuItem(menu, 'Frango na Brasa');
    const defaultModifierIds = frango.modifierGroups
      .filter((g) => g.isRequired)
      .map((g) => g.modifiers[0]?.id)
      .filter((id): id is string => id !== undefined);

    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const afterImperial = await addLine(request, order.id, imperial.id, 2); // 2 Imperiais, one line
    const withFrango = await addLine(request, order.id, frango.id, 1, defaultModifierIds);

    const imperialLine = afterImperial.lines.find((l) => l.itemName === 'Imperial')!;
    const frangoLine = withFrango.lines.find((l) => l.itemName === 'Frango na Brasa')!;

    const discounted = await setLineDiscount(request, order.id, imperialLine.id, 'Percentage', 50);
    const discountedImperialTotal = discounted.lines.find((l) => l.id === imperialLine.id)!.lineTotal.amount;
    const voided = await voidLine(request, order.id, frangoLine.id, 'Kitchen ran out');
    const voidedFrangoTotal = voided.lines.find((l) => l.id === frangoLine.id)!.lineTotal.amount;
    expect(voidedFrangoTotal).toBe(0);

    // Guest A: 1 of the 2 (now half-price) Imperiais. Guest B: the other
    // Imperial half, plus the (now voided, worthless) Frango.
    const result = await previewSplitByItem(request, order.id, [
      { lines: [{ lineId: imperialLine.id, quantity: 1 }] },
      { lines: [{ lineId: imperialLine.id, quantity: 1 }, { lineId: frangoLine.id, quantity: 1 }] },
    ]);

    const [groupA, groupB] = result.groups;
    const imperialShareA = groupA.lines.find((l) => l.lineId === imperialLine.id)!.total.amount;
    const imperialShareB = groupB.lines.find((l) => l.lineId === imperialLine.id)!.total.amount;
    const frangoShareB = groupB.lines.find((l) => l.lineId === frangoLine.id)!.total.amount;

    // Each unit's share reflects the line's own discounted total, not the raw menu price.
    expect(imperialShareA + imperialShareB).toBeCloseTo(discountedImperialTotal, 2);
    expect(imperialShareA).toBeCloseTo(imperialShareB, 2);
    expect(frangoShareB).toBe(0);

    // Internal consistency: each group's stated total is exactly the sum of
    // its own line portions, and both groups sum to the order's own total.
    const groupASum = groupA.lines.reduce((sum, l) => sum + l.total.amount, 0);
    const groupBSum = groupB.lines.reduce((sum, l) => sum + l.total.amount, 0);
    expect(groupA.total.amount).toBeCloseTo(groupASum, 2);
    expect(groupB.total.amount).toBeCloseTo(groupBSum, 2);
    const orderAfter = await getOrder(request, order.id);
    expect(groupA.total.amount + groupB.total.amount).toBeCloseTo(orderAfter.total.amount, 2);

    await closeOrderAndClearTable(request, order.id, table.id);
  });

  test('rejects empty groups, an unknown line, over-allocation and partial allocation', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const withLine = await addLine(request, order.id, imperial.id, 2);
    const [lineId] = withLine.lines.map((l) => l.id);

    const noGroups = await previewSplitByItemResponse(request, order.id, []);
    expect(noGroups.status()).toBe(400);
    expect((await noGroups.json()).code).toBe('order.invalid_split');

    const emptyGroup = await previewSplitByItemResponse(request, order.id, [{ lines: [] }]);
    expect(emptyGroup.status()).toBe(400);
    expect((await emptyGroup.json()).code).toBe('order.invalid_split');

    const unknownLine = await previewSplitByItemResponse(request, order.id, [
      { lines: [{ lineId: crypto.randomUUID(), quantity: 1 }] },
    ]);
    expect(unknownLine.status()).toBe(404);
    expect((await unknownLine.json()).code).toBe('order.line_not_found');

    const overAllocated = await previewSplitByItemResponse(request, order.id, [
      { lines: [{ lineId, quantity: 3 }] }, // only 2 were ordered
    ]);
    expect(overAllocated.status()).toBe(400);
    expect((await overAllocated.json()).code).toBe('order.invalid_split');

    const partiallyAllocated = await previewSplitByItemResponse(request, order.id, [
      { lines: [{ lineId, quantity: 1 }] }, // 1 of 2 left unaccounted for
    ]);
    expect(partiallyAllocated.status()).toBe(400);
    expect((await partiallyAllocated.json()).code).toBe('order.invalid_split');

    await closeOrderAndClearTable(request, order.id, table.id);
  });
});
