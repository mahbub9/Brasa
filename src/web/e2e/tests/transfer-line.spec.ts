import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  findMenuItem,
  getMenu,
  getOrder,
  openOrderOnAnyFreeTable,
  transferLine,
  transferLineResponse,
} from './support/api';

// ORD-13 — moving a single line onto a different open order, e.g. a dish
// following a guest who joins another table, or splitting a large party
// across two tables mid-service. Unlike ORD-12 (whole-order table transfer),
// this never touches Floor at all — both tables stay exactly as occupied as
// they were, only the line moves between the two Ordering aggregates.
//
// No pos UI yet: the screen only ever shows one open order at a time, and
// picking *another* currently-open order is a real product-design question
// (search by table? a mini list?) that shouldn't be answered by inventing a
// UI nobody asked for. Same scoping call already made for ORD-22 (order
// history has no UI either). Backend + API-level coverage only for now.

test.describe('line transfer', () => {
  test('moves a line to a different order, updating both totals', async ({ request }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');
    const frango = findMenuItem(menu, 'Frango na Brasa');

    const { order: source, table: sourceTable } = await openOrderOnAnyFreeTable(request, 2);
    const { order: destination, table: destinationTable } = await openOrderOnAnyFreeTable(request, 2);

    const sourceWithLines = await addLine(request, source.id, imperial.id, 1);
    const [lineId] = sourceWithLines.lines.map((l) => l.id);

    // Frango na Brasa carries a required modifier group — use the same
    // zero-price default other specs rely on instead of guessing an id.
    const defaultModifierIds = frango.modifierGroups
      .filter((g) => g.isRequired)
      .map((g) => g.modifiers[0]?.id)
      .filter((id): id is string => id !== undefined);
    const destinationSeed = await addLine(request, destination.id, frango.id, 1, defaultModifierIds);

    const result = await transferLine(request, source.id, lineId, destination.id);

    expect(result.sourceOrder.lines.some((l) => l.id === lineId)).toBe(false);
    expect(result.destinationOrder.lines.some((l) => l.id === lineId)).toBe(true);
    expect(result.sourceOrder.total.amount).toBeCloseTo(0, 2);
    expect(result.destinationOrder.total.amount).toBeCloseTo(destinationSeed.total.amount + imperial.price.amount, 2);

    // Persisted, not just in the response — re-fetch both independently.
    const sourceRefetched = await getOrder(request, source.id);
    const destinationRefetched = await getOrder(request, destination.id);
    expect(sourceRefetched.lines.some((l) => l.id === lineId)).toBe(false);
    expect(destinationRefetched.lines.some((l) => l.id === lineId)).toBe(true);

    // Cleanup: source has no lines left, so close the destination first
    // (it has lines) then just clear the now-empty source's table directly.
    await closeOrderAndClearTable(request, destination.id, destinationTable.id);
    await addLine(request, source.id, imperial.id, 1); // give it something to close with
    await closeOrderAndClearTable(request, source.id, sourceTable.id);
  });

  test('rejects the same order as destination, an unknown line, an unknown destination, and a closed order on either side', async ({
    request,
  }) => {
    const menu = await getMenu(request);
    const imperial = findMenuItem(menu, 'Imperial');

    const { order: source, table: sourceTable } = await openOrderOnAnyFreeTable(request, 1);
    const { order: destination, table: destinationTable } = await openOrderOnAnyFreeTable(request, 1);
    const sourceWithLine = await addLine(request, source.id, imperial.id, 1);
    const [lineId] = sourceWithLine.lines.map((l) => l.id);

    const ontoSelf = await transferLineResponse(request, source.id, lineId, source.id);
    expect(ontoSelf.status()).toBe(400);
    expect((await ontoSelf.json()).code).toBe('order.invalid_transfer_target');

    const unknownLine = await transferLineResponse(request, source.id, crypto.randomUUID(), destination.id);
    expect(unknownLine.status()).toBe(404);
    expect((await unknownLine.json()).code).toBe('order.line_not_found');

    const unknownDestination = await transferLineResponse(request, source.id, lineId, crypto.randomUUID());
    expect(unknownDestination.status()).toBe(404);
    expect((await unknownDestination.json()).code).toBe('order.not_found');

    await addLine(request, destination.id, imperial.id, 1);
    await closeOrderAndClearTable(request, destination.id, destinationTable.id);

    const ontoClosedDestination = await transferLineResponse(request, source.id, lineId, destination.id);
    expect(ontoClosedDestination.status()).toBe(409);
    expect((await ontoClosedDestination.json()).code).toBe('order.not_open');

    await closeOrderAndClearTable(request, source.id, sourceTable.id);

    const { order: freshDestination, table: freshDestinationTable } = await openOrderOnAnyFreeTable(request, 1);
    const fromClosedSource = await transferLineResponse(request, source.id, lineId, freshDestination.id);
    expect(fromClosedSource.status()).toBe(409);
    expect((await fromClosedSource.json()).code).toBe('order.not_open');

    await addLine(request, freshDestination.id, imperial.id, 1);
    await closeOrderAndClearTable(request, freshDestination.id, freshDestinationTable.id);
  });
});
