import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  findMenuItem,
  getFloor,
  getMenu,
  getOrder,
  mergeOrders,
  mergeOrdersResponse,
  openOrderOnAnyFreeTable,
} from './support/api';

// ORD-14 — merging a secondary open order's lines into a primary one, e.g.
// two parties combining onto one table. The secondary order ends up
// Merged (not Closed — no fiscal document was ever issued for it) and its
// table frees directly, the same as ORD-12's old table: nothing was billed
// there, so it never passes through Dirty.
//
// No pos UI yet, for the same reason as ORD-13: picking *another*
// currently-open order to merge with is a real product-design question,
// not something to answer by inventing an unrequested UI. API-level
// coverage only.

test.describe('merge orders', () => {
  test('moves every line into the primary order and frees the secondary table', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const frango = findMenuItem(menu, 'Frango na Brasa');
    const defaultModifierIds = frango.modifierGroups
      .filter((g) => g.isRequired)
      .map((g) => g.modifiers[0]?.id)
      .filter((id): id is string => id !== undefined);

    const { order: primaryOpened, table: primaryTable } = await openOrderOnAnyFreeTable(request, 2);
    const { order: secondaryOpened, table: secondaryTable } = await openOrderOnAnyFreeTable(request, 2);

    const primaryWithLine = await addLine(request, primaryOpened.id, imperial.id, 1);
    const secondaryWithLine = await addLine(request, secondaryOpened.id, frango.id, 1, defaultModifierIds);
    const [secondaryLineId] = secondaryWithLine.lines.map((l) => l.id);

    const merged = await mergeOrders(request, primaryOpened.id, secondaryOpened.id);

    expect(merged.primaryOrder.lines.some((l) => l.id === secondaryLineId)).toBe(true);
    expect(merged.primaryOrder.total.amount).toBeCloseTo(
      primaryWithLine.total.amount + secondaryWithLine.total.amount,
      2,
    );
    expect(merged.secondaryOrder.lines).toHaveLength(0);
    expect(merged.secondaryOrder.status).toBe('Merged');

    // Persisted, not just in the response.
    const primaryRefetched = await getOrder(request, primaryOpened.id);
    expect(primaryRefetched.lines.some((l) => l.id === secondaryLineId)).toBe(true);
    expect(primaryRefetched.lines).toHaveLength(2);

    // The secondary's table went straight back to Free — no billing
    // happened there, so it must never pass through Dirty.
    const floorAfter = await getFloor(request);
    const secondaryTableAfter = floorAfter.flatMap((r) => r.tables).find((t) => t.id === secondaryTable.id);
    expect(secondaryTableAfter?.state).toBe('Free');

    await closeOrderAndClearTable(request, primaryOpened.id, primaryTable.id);
  });

  test('rejects merging into itself, an unknown secondary, and either side already closed', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');

    const { order: primary, table: primaryTable } = await openOrderOnAnyFreeTable(request, 1);
    const { order: secondary, table: secondaryTable } = await openOrderOnAnyFreeTable(request, 1);

    const ontoSelf = await mergeOrdersResponse(request, primary.id, primary.id);
    expect(ontoSelf.status()).toBe(400);
    expect((await ontoSelf.json()).code).toBe('order.invalid_merge_target');

    const unknownSecondary = await mergeOrdersResponse(request, primary.id, crypto.randomUUID());
    expect(unknownSecondary.status()).toBe(404);
    expect((await unknownSecondary.json()).code).toBe('order.not_found');

    await addLine(request, primary.id, imperial.id, 1);
    await closeOrderAndClearTable(request, primary.id, primaryTable.id);

    const primaryClosed = await mergeOrdersResponse(request, primary.id, secondary.id);
    expect(primaryClosed.status()).toBe(409);
    expect((await primaryClosed.json()).code).toBe('order.not_open');

    const { order: freshPrimary, table: freshPrimaryTable } = await openOrderOnAnyFreeTable(request, 1);
    await addLine(request, secondary.id, imperial.id, 1);
    await closeOrderAndClearTable(request, secondary.id, secondaryTable.id);

    const secondaryClosed = await mergeOrdersResponse(request, freshPrimary.id, secondary.id);
    expect(secondaryClosed.status()).toBe(409);
    expect((await secondaryClosed.json()).code).toBe('order.not_open');

    await addLine(request, freshPrimary.id, imperial.id, 1);
    await closeOrderAndClearTable(request, freshPrimary.id, freshPrimaryTable.id);
  });
});
