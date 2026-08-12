import { expect, test } from '@playwright/test';
import { createRoom, deleteRoomResponse, getFloor, updateRoomResponse } from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// FLR-07 — a room's FloorLevel (0 ground floor, positive above it,
// negative below), letting a multi-storey restaurant's floor plan say
// which physical level a room sits on. Most seeded rooms stay at the
// default 0, so this file is the only place in the suite that ever
// creates a room on a different floor — the badge/heading it turns on is
// deliberately invisible everywhere else (see FloorManager/TablePicker's
// own remarks) until more than one floor level actually exists.

test.describe('room floor level (FLR-07)', () => {
  test('round-trips floorLevel through create, GET /floor, and update', async ({ request }) => {
    const name = `Floor Level Test ${Date.now()}`;
    const created = await createRoom(request, { name, displayOrder: 99, floorLevel: 1 });
    expect(created.floorLevel).toBe(1);

    const afterCreate = await getFloor(request);
    expect(afterCreate.find((r) => r.id === created.id)?.floorLevel).toBe(1);

    const updated = await updateRoomResponse(request, created.id, { name, displayOrder: 99, floorLevel: -1 });
    expect(updated.status()).toBe(200);
    expect((await updated.json()).floorLevel).toBe(-1);

    const afterUpdate = await getFloor(request);
    expect(afterUpdate.find((r) => r.id === created.id)?.floorLevel).toBe(-1);

    await deleteRoomResponse(request, created.id);
  });

  test('creating a room without floorLevel defaults to 0, the ground floor', async ({ request }) => {
    const name = `Floor Level Default Test ${Date.now()}`;
    const created = await createRoom(request, { name, displayOrder: 99 });
    expect(created.floorLevel).toBe(0);
    await deleteRoomResponse(request, created.id);
  });

  test('the admin floor manager shows a floor badge on every room once more than one floor exists', async ({
    page,
    request,
  }) => {
    const upstairsName = `Floor Badge Test ${Date.now()}`;
    const upstairs = await createRoom(request, { name: upstairsName, displayOrder: 99, floorLevel: 1 });

    const rooms = await getFloor(request);
    const groundFloorRoom = rooms.find((r) => r.floorLevel === 0 && r.id !== upstairs.id);
    if (!groundFloorRoom) throw new Error('No seeded ground-floor room found for this test.');

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-floor').click();
    await page.waitForSelector('.floor-manager');

    const upstairsBadge = page.getByTestId(`room-floor-badge-${upstairsName}`);
    await upstairsBadge.scrollIntoViewIfNeeded();
    await expect(upstairsBadge).toBeVisible();
    await expect(upstairsBadge).toContainText('1');

    // Once ambiguity exists, every room gets labelled, not just the odd
    // one out — otherwise "no badge" would be a second, silent way to
    // mean "ground floor," indistinguishable from "not built yet."
    const groundFloorBadge = page.getByTestId(`room-floor-badge-${groundFloorRoom.name}`);
    await groundFloorBadge.scrollIntoViewIfNeeded();
    await expect(groundFloorBadge).toBeVisible();
    await expect(groundFloorBadge).toContainText('0');

    await deleteRoomResponse(request, upstairs.id);
  });

  test('the admin floor manager edits a room onto a different floor', async ({ page, request }) => {
    const name = `Floor Edit Test ${Date.now()}`;
    const created = await createRoom(request, { name, displayOrder: 99, floorLevel: 0 });

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-floor').click();
    await page.waitForSelector('.floor-manager');

    const room = page.getByTestId(`room-${name}`);
    await room.scrollIntoViewIfNeeded();
    await room.getByTestId(`room-name-edit-${name}`).click();
    await room.getByTestId(`room-floor-level-input-${name}`).fill('2');
    await room.getByTestId(`room-name-save-${name}`).click();

    await expect(room.getByTestId(`room-floor-badge-${name}`)).toContainText('2');

    const afterEdit = await getFloor(request);
    expect(afterEdit.find((r) => r.id === created.id)?.floorLevel).toBe(2);

    await deleteRoomResponse(request, created.id);
  });

  test('pos groups the table picker by floor once more than one floor exists', async ({ page, request }) => {
    const upstairsName = `Pos Floor Test ${Date.now()}`;
    const upstairs = await createRoom(request, { name: upstairsName, displayOrder: 99, floorLevel: 1 });

    await page.goto('/');
    await page.waitForSelector('.table-picker');

    await expect(page.getByTestId('floor-level-0')).toBeVisible();
    await expect(page.getByTestId('floor-level-1')).toBeVisible();
    await expect(page.getByText(upstairsName)).toBeVisible();

    await deleteRoomResponse(request, upstairs.id);
  });
});
