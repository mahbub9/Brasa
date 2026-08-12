import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  createTableGroup,
  createTableGroupResponse,
  deleteTableGroup,
  deleteTableGroupResponse,
  findFreeTables,
  findMenuItem,
  getFloor,
  getMenu,
  openOrderResponse,
} from './support/api';

// FLR-05 — pushing free tables together into one seating group. No client
// consumes this yet (no floor-plan multi-select UI exists in admin/pos
// today), so this is API-only, same "ship the mechanism ahead of the
// trigger" pattern as CAT-05/CAT-10/CAT-16. The mechanism has real teeth:
// Table.Occupy() itself refuses a grouped table (floor.table_grouped) —
// see that method's own remarks for why cascading Occupy/Clear was
// deliberately deferred instead.

test.describe('table groups (FLR-05)', () => {
  test('groups free tables, blocks seating them individually, then ungroups', async ({ request }) => {
    const imperial = findMenuItem(await getMenu(request), 'Imperial');
    const [a, b] = findFreeTables(await getFloor(request), 2);

    const grouped = await createTableGroup(request, [a.id, b.id]);
    expect(grouped).toHaveLength(2);
    const groupId = grouped[0].groupId;
    expect(groupId).toBeTruthy();
    expect(grouped.every((t) => t.groupId === groupId)).toBe(true);

    const afterGroup = await getFloor(request);
    const tablesAfterGroup = afterGroup.flatMap((r) => r.tables);
    expect(tablesAfterGroup.find((t) => t.id === a.id)?.groupId).toBe(groupId);
    expect(tablesAfterGroup.find((t) => t.id === b.id)?.groupId).toBe(groupId);

    const blockedA = await openOrderResponse(request, a.id, 2);
    expect(blockedA.status()).toBe(409);
    expect((await blockedA.json()).code).toBe('floor.table_grouped');

    const blockedB = await openOrderResponse(request, b.id, 2);
    expect(blockedB.status()).toBe(409);
    expect((await blockedB.json()).code).toBe('floor.table_grouped');

    const ungroupResponse = await deleteTableGroupResponse(request, groupId!);
    expect(ungroupResponse.status()).toBe(204);

    const afterUngroup = await getFloor(request);
    const tablesAfterUngroup = afterUngroup.flatMap((r) => r.tables);
    expect(tablesAfterUngroup.find((t) => t.id === a.id)?.groupId).toBeNull();
    expect(tablesAfterUngroup.find((t) => t.id === b.id)?.groupId).toBeNull();

    // Now ungrouped, ordinary seating works again — confirms the block was
    // caused by grouping and not by some other table-state side effect.
    const allowed = await openOrderResponse(request, a.id, 2);
    expect(allowed.ok()).toBe(true);
    const order = await allowed.json();

    await addLine(request, order.id, imperial.id, 1);
    await closeOrderAndClearTable(request, order.id, a.id);
  });

  test('rejects too few tables, an unknown table, a non-free table and an already-grouped table', async ({
    request,
  }) => {
    // Grabbed once, up front: grouping doesn't change a table's State (only
    // Occupy() cares about GroupId), so a *second* findFreeTables call mid-test
    // could hand back an already-grouped table by mistake. Fixing the pool up
    // front sidesteps that entirely.
    const imperial = findMenuItem(await getMenu(request), 'Imperial');
    const [single, x, c, d, e, f] = findFreeTables(await getFloor(request), 6);

    const tooFew = await createTableGroupResponse(request, [single.id]);
    expect(tooFew.status()).toBe(400);
    expect((await tooFew.json()).code).toBe('floor.table_group_too_small');

    const unknownTable = await createTableGroupResponse(request, [x.id, crypto.randomUUID()]);
    expect(unknownTable.status()).toBe(404);
    expect((await unknownTable.json()).code).toBe('floor.table_not_found');

    const opened = await openOrderResponse(request, c.id, 2);
    expect(opened.ok()).toBe(true);
    const order = await opened.json();

    const notFree = await createTableGroupResponse(request, [c.id, d.id]);
    expect(notFree.status()).toBe(409);
    expect((await notFree.json()).code).toBe('floor.table_not_free');

    await addLine(request, order.id, imperial.id, 1);
    await closeOrderAndClearTable(request, order.id, c.id);

    const already = await createTableGroup(request, [d.id, e.id]);
    const alreadyGrouped = await createTableGroupResponse(request, [d.id, f.id]);
    expect(alreadyGrouped.status()).toBe(409);
    expect((await alreadyGrouped.json()).code).toBe('floor.table_already_grouped');

    await deleteTableGroup(request, already[0].groupId!);
  });

  test('rejects deleting an unknown table group', async ({ request }) => {
    const response = await deleteTableGroupResponse(request, crypto.randomUUID());
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('floor.table_group_not_found');
  });
});
