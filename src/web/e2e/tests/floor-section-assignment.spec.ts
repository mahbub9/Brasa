import { expect, test } from '@playwright/test';
import {
  assignRoomSection,
  assignRoomSectionResponse,
  createRoom,
  deleteRoomResponse,
  getFloor,
  getOrganizations,
  getSites,
  getStaff,
} from './support/api';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// FLR-06 — assigning a waiter to a room as their section, unblocked by this
// session's own IDN-01 (Site)/IDN-08-09 (Staff) work. A room is the
// existing "which area" granularity (Salão, Esplanada) — no separate
// Section entity, the same "no entity where a plain field says the same
// thing" call FLR-05's Table.GroupId and FLR-07's Room.FloorLevel already
// made. Any staff role can be assigned, not just Manager — unlike IDN-11's
// authorisation gate, this isn't a privileged action.

async function getDemoStaff(request: import('@playwright/test').APIRequestContext) {
  const organizations = await getOrganizations(request);
  const org = organizations[0];
  if (!org) throw new Error('No seeded organization found for this test.');
  const sites = await getSites(request, org.id);
  const site = sites[0];
  if (!site) throw new Error('No seeded site found for this test.');
  return getStaff(request, site.id);
}

test.describe('room section assignment (FLR-06)', () => {
  test('assigns a section to a plain Staff-role member (not manager-only), resolving the name; clearing removes it', async ({
    request,
  }) => {
    const staff = await getDemoStaff(request);
    const waiter = staff.find((s) => s.role === 'Staff');
    if (!waiter) throw new Error('No seeded Staff-role member found — did DevIdentitySeeder change?');

    const name = `Section Test ${Date.now()}`;
    const room = await createRoom(request, { name, displayOrder: 99 });
    expect(room.assignedStaffId).toBeNull();
    expect(room.assignedStaffName).toBeNull();

    const assigned = await assignRoomSection(request, room.id, waiter.id);
    expect(assigned.assignedStaffId).toBe(waiter.id);
    expect(assigned.assignedStaffName).toBe(waiter.name);

    const viaFloor = await getFloor(request);
    const onFloor = viaFloor.find((r) => r.id === room.id);
    expect(onFloor?.assignedStaffId).toBe(waiter.id);
    expect(onFloor?.assignedStaffName).toBe(waiter.name);

    const cleared = await assignRoomSection(request, room.id, null);
    expect(cleared.assignedStaffId).toBeNull();
    expect(cleared.assignedStaffName).toBeNull();

    await deleteRoomResponse(request, room.id);
  });

  test('rejects an unknown staff id and an unknown room', async ({ request }) => {
    const name = `Section Rejection Test ${Date.now()}`;
    const room = await createRoom(request, { name, displayOrder: 99 });

    const unknownStaff = await assignRoomSectionResponse(request, room.id, crypto.randomUUID());
    expect(unknownStaff.status()).toBe(404);
    expect((await unknownStaff.json()).code).toBe('identity.staff_not_found');

    const unknownRoom = await assignRoomSectionResponse(request, crypto.randomUUID(), null);
    expect(unknownRoom.status()).toBe(404);
    expect((await unknownRoom.json()).code).toBe('floor.room_not_found');

    await deleteRoomResponse(request, room.id);
  });

  test('the admin floor manager assigns and clears a section through the real UI', async ({ page, request }) => {
    const staff = await getDemoStaff(request);
    const manager = staff.find((s) => s.role === 'Manager');
    if (!manager) throw new Error('No seeded Manager-role member found — did DevIdentitySeeder change?');

    const name = `Section UI Test ${Date.now()}`;
    const room = await createRoom(request, { name, displayOrder: 99 });

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-floor').click();
    await page.waitForSelector('.floor-manager');

    const roomSection = page.getByTestId(`room-${name}`);
    await roomSection.scrollIntoViewIfNeeded();
    const select = roomSection.getByTestId(`room-section-${name}`);
    await select.selectOption(manager.id);

    // Not just "the UI shows a selection" -- confirm it actually took effect.
    await expect
      .poll(async () => {
        const floor = await getFloor(request);
        return floor.find((r) => r.id === room.id)?.assignedStaffId ?? null;
      })
      .toBe(manager.id);

    await select.selectOption('');
    await expect
      .poll(async () => {
        const floor = await getFloor(request);
        return floor.find((r) => r.id === room.id)?.assignedStaffId ?? null;
      })
      .toBeNull();

    await deleteRoomResponse(request, room.id);
  });
});
