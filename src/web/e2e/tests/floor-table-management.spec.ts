import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrder,
  createTable,
  createTableResponse,
  deleteTableResponse,
  getFloor,
  getMenu,
  openOrderOnAnyFreeTable,
  updateTable,
  updateTableResponse,
} from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// FLR-03's first slice — table CRUD (add/edit/delete), not yet the
// drag-and-drop canvas this backlog row's own title names. Position/shape
// were seeded onto Table since I1 for exactly this (see that type's own
// remarks); admin submits plain numeric position fields today, no visual
// editor. Room creation stays a deliberate, separate gap — tables can only
// be added to a room that already exists (seeded, FLR-01).

test.describe('table CRUD (FLR-03)', () => {
  test('creates, edits and deletes a table via the API', async ({ request }) => {
    const rooms = await getFloor(request);
    const room = rooms[0];

    const created = await createTable(request, room.id, {
      label: `CRUD Test ${Date.now()}`,
      seats: 4,
      positionX: 10,
      positionY: 20,
      shape: 'Square',
    });
    expect(created.roomId).toBe(room.id);
    expect(created.seats).toBe(4);
    expect(created.shape).toBe('Square');
    expect(created.state).toBe('Free');

    const afterCreate = await getFloor(request);
    const foundAfterCreate = afterCreate.flatMap((r) => r.tables).find((t) => t.id === created.id);
    expect(foundAfterCreate).toBeTruthy();

    const updated = await updateTable(request, created.id, {
      label: `CRUD Test Renamed ${Date.now()}`,
      seats: 6,
      positionX: 15,
      positionY: 25,
      shape: 'Round',
    });
    expect(updated.seats).toBe(6);
    expect(updated.shape).toBe('Round');
    expect(updated.positionX).toBe(15);
    expect(updated.positionY).toBe(25);

    const deleteResponse = await deleteTableResponse(request, created.id);
    expect(deleteResponse.status()).toBe(204);

    const afterDelete = await getFloor(request);
    expect(afterDelete.flatMap((r) => r.tables).some((t) => t.id === created.id)).toBe(false);
  });

  test('rejects an empty label, zero seats, an unrecognised shape and an unknown room', async ({ request }) => {
    const rooms = await getFloor(request);
    const room = rooms[0];
    const valid = { label: 'X', seats: 2, positionX: 0, positionY: 0, shape: 'Round' };

    const emptyLabel = await createTableResponse(request, room.id, { ...valid, label: '' });
    expect(emptyLabel.status()).toBe(400);
    expect((await emptyLabel.json()).code).toBe('floor.invalid_label');

    const zeroSeats = await createTableResponse(request, room.id, { ...valid, seats: 0 });
    expect(zeroSeats.status()).toBe(400);
    expect((await zeroSeats.json()).code).toBe('floor.invalid_seats');

    const badShape = await createTableResponse(request, room.id, { ...valid, shape: 'Triangle' });
    expect(badShape.status()).toBe(400);
    expect((await badShape.json()).code).toBe('floor.invalid_shape');

    const badRoom = await createTableResponse(request, crypto.randomUUID(), valid);
    expect(badRoom.status()).toBe(404);
    expect((await badRoom.json()).code).toBe('floor.room_not_found');
  });

  test('rejects updating or deleting an unknown table', async ({ request }) => {
    const valid = { label: 'X', seats: 2, positionX: 0, positionY: 0, shape: 'Round' };

    const updateUnknown = await updateTableResponse(request, crypto.randomUUID(), valid);
    expect(updateUnknown.status()).toBe(404);
    expect((await updateUnknown.json()).code).toBe('floor.table_not_found');

    const deleteUnknown = await deleteTableResponse(request, crypto.randomUUID());
    expect(deleteUnknown.status()).toBe(404);
    expect((await deleteUnknown.json()).code).toBe('floor.table_not_found');
  });

  test('rejects deleting a table that is not free', async ({ request }) => {
    const menu = await getMenu(request);
    const item = menu.flatMap((c) => c.items).find((i) => i.modifierGroups.length === 0);
    if (!item) throw new Error('No modifier-free menu item found for this test.');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const guarded = await deleteTableResponse(request, table.id);
    expect(guarded.status()).toBe(409);
    expect((await guarded.json()).code).toBe('floor.table_not_free');

    // Cleanup: close normally so the table returns to the free pool for
    // other specs, rather than leaving it stuck occupied.
    await addLine(request, order.id, item.id, 1);
    await closeOrder(request, order.id);
  });

  test('the admin editor adds, edits and deletes a table', async ({ page, request }) => {
    const rooms = await getFloor(request);
    const roomName = rooms[0].name;
    const label = `Admin UI Test ${Date.now()}`;

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-floor').click();
    await page.waitForSelector('.floor-manager');

    const room = page.getByTestId(`room-${roomName}`);
    await room.scrollIntoViewIfNeeded();

    await room.getByTestId(`add-table-${rooms[0].id}`).click();
    await room.getByTestId(`new-table-label-${rooms[0].id}`).fill(label);
    await room.getByTestId(`new-table-seats-${rooms[0].id}`).fill('3');
    await room.getByTestId(`new-table-shape-${rooms[0].id}`).selectOption('Square');
    await room.getByTestId(`new-table-save-${rooms[0].id}`).click();

    // Admin defaults to Portuguese (same brasa.lang cookie convention as pos).
    const row = page.getByTestId(`table-${label}`);
    await expect(row).toBeVisible();
    await expect(row).toContainText('3 lugares');
    await expect(row).toContainText('Quadrada');

    await row.getByTestId(`table-edit-${label}`).click();
    await row.getByTestId(`table-seats-input-${label}`).fill('5');
    await row.getByTestId(`table-shape-input-${label}`).selectOption('Round');
    await row.getByTestId(`table-save-${label}`).click();
    await expect(row).toContainText('5 lugares');
    await expect(row).toContainText('Redonda');

    await row.getByTestId(`table-delete-${label}`).click();
    await row.getByTestId(`table-delete-confirm-${label}`).click();
    await expect(row).toHaveCount(0);
  });
});
