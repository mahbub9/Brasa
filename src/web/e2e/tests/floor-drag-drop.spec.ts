import { expect, test } from '@playwright/test';
import { createRoom, createTable, deleteRoomResponse, deleteTableResponse, getFloor } from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// FLR-03's drag-and-drop half — the canvas (FloorCanvas, admin's
// FloorManager.tsx) is a mouse-only convenience layer over the exact same
// PUT /tables/{id} the plain number-input form already exercises
// (floor-table-management.spec.ts); this spec proves the pointer-drag
// interaction itself commits a real, grid-snapped position change, not
// that position-setting works at all — that's already covered.
//
// Uses an isolated room+table, never a seeded one — dragging a shared
// demo table around would race with whichever spec currently depends on
// its position or state, same reasoning as floor-section-assignment.spec.ts.

test.describe('floor plan drag-and-drop canvas (FLR-03)', () => {
  test('dragging a table on the canvas persists its new grid position', async ({ page, request }) => {
    const room = await createRoom(request, { name: `Drag Test Room ${Date.now()}`, displayOrder: 99 });
    const label = `Drag Test ${Date.now()}`;
    const table = await createTable(request, room.id, { label, seats: 2, positionX: 0, positionY: 0, shape: 'Round' });

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-floor').click();
    await page.waitForSelector('.floor-manager');

    const canvasTable = page.getByTestId(`canvas-table-${label}`);
    await canvasTable.scrollIntoViewIfNeeded();
    const box = await canvasTable.boundingBox();
    if (!box) throw new Error('Canvas table has no bounding box — is the canvas rendered?');

    // Drag well past half a cell in both directions (the canvas's own grid
    // cell is 70px) so rounding can never land back on the starting cell —
    // this test doesn't need to know the exact constant, only that the
    // delta is unambiguous.
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2 + 150, box.y + box.height / 2 + 90, { steps: 10 });
    await page.mouse.up();

    await expect
      .poll(async () => {
        const floor = await getFloor(request);
        const updated = floor.flatMap((r) => r.tables).find((t) => t.id === table.id);
        return updated ? [updated.positionX, updated.positionY] : null;
      })
      .toEqual([2, 1]);

    await deleteTableResponse(request, table.id);
    await deleteRoomResponse(request, room.id);
  });

  test('a drag of less than half a cell snaps back to the original position, no update fired', async ({
    page,
    request,
  }) => {
    const room = await createRoom(request, { name: `Drag Snap Test Room ${Date.now()}`, displayOrder: 99 });
    const label = `Drag Snap Test ${Date.now()}`;
    const table = await createTable(request, room.id, { label, seats: 2, positionX: 3, positionY: 3, shape: 'Square' });

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-floor').click();
    await page.waitForSelector('.floor-manager');

    const canvasTable = page.getByTestId(`canvas-table-${label}`);
    await canvasTable.scrollIntoViewIfNeeded();
    const box = await canvasTable.boundingBox();
    if (!box) throw new Error('Canvas table has no bounding box — is the canvas rendered?');

    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2 + 10, box.y + box.height / 2 + 10, { steps: 5 });
    await page.mouse.up();

    // A tiny nudge rounds back to the same grid cell — no PUT should even
    // fire. Give a wrongly-fired request a moment to land before asserting
    // nothing changed.
    await page.waitForTimeout(300);
    const floor = await getFloor(request);
    const unchanged = floor.flatMap((r) => r.tables).find((t) => t.id === table.id);
    expect(unchanged?.positionX).toBe(3);
    expect(unchanged?.positionY).toBe(3);

    await deleteTableResponse(request, table.id);
    await deleteRoomResponse(request, room.id);
  });
});
