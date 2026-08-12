import type { APIRequestContext } from '@playwright/test';
import type {
  AdminMenuCategoryDto,
  CloseOrderResponse,
  ComboDto,
  EffectivePriceDto,
  MenuCategoryDto,
  MenuItemDto,
  OrderDto,
  OrderSummaryDto,
  OrganizationDto,
  PreBillDto,
  PriceListDto,
  ResolvedTaxRuleDto,
  RoomDto,
  SiteDto,
  SplitByItemResponse,
  TableDto,
  TaxRuleDto,
  TerminalDto,
} from './types';

// Deterministic test-data builders (QA-03) — set up state via the API
// directly instead of clicking through the UI, so specs that aren't
// exercising the ordering flow itself don't pay for it. The happy-path spec
// (walking-skeleton.spec.ts) deliberately does NOT use these — it drives the
// real UI end to end, because that is the one thing worth proving by hand.
//
// The dev database is NOT reset between runs (QA-02's known limitation —
// see docs/development/e2e-testing.md), and there are only 16 seeded tables.
// Every helper here that occupies one is paired with a way to give it back
// (closeOrderAndClearTable), and every spec that opens a table calls it —
// otherwise repeated runs eventually exhaust the free-table pool.

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

function idempotencyKey(): string {
  return crypto.randomUUID();
}

/** Fetches the live seeded menu — never hardcode menu item ids, they are UUIDv7 and not stable across a fresh database. */
export async function getMenu(request: APIRequestContext): Promise<MenuCategoryDto[]> {
  const response = await request.get(`${apiBaseUrl}/menu`);
  if (!response.ok()) {
    throw new Error(`GET /menu failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Every category and item, hidden/unavailable ones included — WEB-10's admin management view (GetMenuAllAsync). */
export async function getMenuAll(request: APIRequestContext): Promise<AdminMenuCategoryDto[]> {
  const response = await request.get(`${apiBaseUrl}/menu/all`);
  if (!response.ok()) {
    throw new Error(`GET /menu/all failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Finds a seeded menu item by its display name, failing loudly if the demo menu ever changes. */
export function findMenuItem(categories: MenuCategoryDto[], name: string) {
  for (const category of categories) {
    const item = category.items.find((i) => i.name === name);
    if (item) return item;
  }
  throw new Error(`No seeded menu item named "${name}" — did DevCatalogSeeder change?`);
}

/**
 * Picks the minimum valid modifier selection for an item — the first
 * (lowest displayOrder) choice from every required group, none from
 * optional ones. Assumes seed data lists its default option first (true for
 * DevCatalogSeeder's "Dose normal" today); this is a test convenience for
 * items that merely *have* required modifiers, not a test of modifier
 * pricing itself — see modifiers.spec.ts for that.
 */
export function defaultRequiredModifierIds(item: MenuItemDto): string[] {
  return item.modifierGroups
    .filter((g) => g.isRequired)
    .map((g) => g.modifiers[0]?.id)
    .filter((id): id is string => id !== undefined);
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-17). */
export function importMenuItemsResponse(request: APIRequestContext, csv: string) {
  return request.post(`${apiBaseUrl}/menu/items/import`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { csv },
  });
}

/** Deletes (soft) a menu item — CAT-18. Used to give an imported test item back when a spec is done with it. */
export async function deleteMenuItem(request: APIRequestContext, itemId: string): Promise<void> {
  const response = await request.delete(`${apiBaseUrl}/menu/items/${itemId}`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
  if (!response.ok()) {
    throw new Error(`DELETE /menu/items/${itemId} failed: ${response.status()} ${await response.text()}`);
  }
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-02). */
export function updateMenuItemDetailsResponse(
  request: APIRequestContext,
  itemId: string,
  description: string | null,
  allergens: string[],
) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/details`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { description, allergens },
  });
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-13). */
export function updateMenuItemAvailabilityResponse(request: APIRequestContext, itemId: string, isAvailable: boolean) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/availability`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { isAvailable },
  });
}

/** Raw response so callers can assert on status/body for the failure cases too. */
export function updateMenuItemPriceResponse(request: APIRequestContext, itemId: string, price: number) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/price`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { price },
  });
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-06). Null clears it. */
export function updateMenuItemTakeawayPriceResponse(request: APIRequestContext, itemId: string, price: number | null) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/takeaway-price`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { price },
  });
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-01). */
export function updateMenuCategoryVisibilityResponse(request: APIRequestContext, categoryId: string, isVisible: boolean) {
  return request.put(`${apiBaseUrl}/menu/categories/${categoryId}/visibility`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { isVisible },
  });
}

export async function updateMenuItemDetails(
  request: APIRequestContext,
  itemId: string,
  description: string | null,
  allergens: string[],
): Promise<MenuItemDto> {
  const response = await updateMenuItemDetailsResponse(request, itemId, description, allergens);
  if (!response.ok()) {
    throw new Error(`PUT /menu/items/${itemId}/details failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-14). */
export function updateMenuItemCourseResponse(request: APIRequestContext, itemId: string, course: string | null) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/course`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { course },
  });
}

export async function updateMenuItemCourse(
  request: APIRequestContext,
  itemId: string,
  course: string | null,
): Promise<MenuItemDto> {
  const response = await updateMenuItemCourseResponse(request, itemId, course);
  if (!response.ok()) {
    throw new Error(`PUT /menu/items/${itemId}/course failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-15). */
export function updateMenuItemStationResponse(request: APIRequestContext, itemId: string, station: string | null) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/station`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { station },
  });
}

export async function updateMenuItemStation(
  request: APIRequestContext,
  itemId: string,
  station: string | null,
): Promise<MenuItemDto> {
  const response = await updateMenuItemStationResponse(request, itemId, station);
  if (!response.ok()) {
    throw new Error(`PUT /menu/items/${itemId}/station failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-11). All three null clears the schedule. */
export function updateMenuItemScheduleResponse(
  request: APIRequestContext,
  itemId: string,
  daysOfWeek: string[] | null,
  startTime: string | null,
  endTime: string | null,
) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/schedule`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { daysOfWeek, startTime, endTime },
  });
}

export async function updateMenuItemSchedule(
  request: APIRequestContext,
  itemId: string,
  daysOfWeek: string[] | null,
  startTime: string | null,
  endTime: string | null,
): Promise<MenuItemDto> {
  const response = await updateMenuItemScheduleResponse(request, itemId, daysOfWeek, startTime, endTime);
  if (!response.ok()) {
    throw new Error(`PUT /menu/items/${itemId}/schedule failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-12). */
export function updateMenuItemCouvertResponse(request: APIRequestContext, itemId: string, isCouvert: boolean) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/couvert`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { isCouvert },
  });
}

export async function updateMenuItemCouvert(
  request: APIRequestContext,
  itemId: string,
  isCouvert: boolean,
): Promise<MenuItemDto> {
  const response = await updateMenuItemCouvertResponse(request, itemId, isCouvert);
  if (!response.ok()) {
    throw new Error(`PUT /menu/items/${itemId}/couvert failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-16). Both null clears the pending change. */
export function updateMenuItemScheduledPriceResponse(
  request: APIRequestContext,
  itemId: string,
  price: number | null,
  effectiveFromUtc: string | null,
) {
  return request.put(`${apiBaseUrl}/menu/items/${itemId}/scheduled-price`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { price, effectiveFromUtc },
  });
}

export async function updateMenuItemScheduledPrice(
  request: APIRequestContext,
  itemId: string,
  price: number | null,
  effectiveFromUtc: string | null,
): Promise<MenuItemDto> {
  const response = await updateMenuItemScheduledPriceResponse(request, itemId, price, effectiveFromUtc);
  if (!response.ok()) {
    throw new Error(`PUT /menu/items/${itemId}/scheduled-price failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Fetches the live seeded floor plan. */
export async function getFloor(request: APIRequestContext): Promise<RoomDto[]> {
  const response = await request.get(`${apiBaseUrl}/floor`);
  if (!response.ok()) {
    throw new Error(`GET /floor failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Picks any currently-free table — never hardcode a table id, they are UUIDv7. */
export function findFreeTable(rooms: RoomDto[]): TableDto {
  for (const room of rooms) {
    const table = room.tables.find((t) => t.state === 'Free');
    if (table) return table;
  }
  throw new Error('No free table available — the seeded floor plan may be exhausted. See DevFloorSeeder.');
}

/** Picks `count` distinct currently-free tables (FLR-05's table groups need 2+ at once). */
export function findFreeTables(rooms: RoomDto[], count: number): TableDto[] {
  const free = rooms.flatMap((r) => r.tables).filter((t) => t.state === 'Free');
  if (free.length < count) {
    throw new Error(`Only ${free.length} free table(s) available, needed ${count}. See DevFloorSeeder.`);
  }
  return free.slice(0, count);
}

interface TableFields {
  label: string;
  seats: number;
  positionX: number;
  positionY: number;
  shape: string;
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-03). */
export function createTableResponse(request: APIRequestContext, roomId: string, fields: TableFields) {
  return request.post(`${apiBaseUrl}/rooms/${roomId}/tables`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: fields,
  });
}

export async function createTable(request: APIRequestContext, roomId: string, fields: TableFields): Promise<TableDto> {
  const response = await createTableResponse(request, roomId, fields);
  if (!response.ok()) {
    throw new Error(`POST /rooms/${roomId}/tables failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-03). */
export function updateTableResponse(request: APIRequestContext, tableId: string, fields: TableFields) {
  return request.put(`${apiBaseUrl}/tables/${tableId}`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: fields,
  });
}

export async function updateTable(request: APIRequestContext, tableId: string, fields: TableFields): Promise<TableDto> {
  const response = await updateTableResponse(request, tableId, fields);
  if (!response.ok()) {
    throw new Error(`PUT /tables/${tableId} failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-03). */
export function deleteTableResponse(request: APIRequestContext, tableId: string) {
  return request.delete(`${apiBaseUrl}/tables/${tableId}`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
}

interface RoomFields {
  name: string;
  displayOrder: number;
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-03's room-CRUD follow-up). */
export function createRoomResponse(request: APIRequestContext, fields: RoomFields) {
  return request.post(`${apiBaseUrl}/rooms`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: fields,
  });
}

export async function createRoom(request: APIRequestContext, fields: RoomFields): Promise<RoomDto> {
  const response = await createRoomResponse(request, fields);
  if (!response.ok()) {
    throw new Error(`POST /rooms failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-03's room-CRUD follow-up). */
export function updateRoomResponse(request: APIRequestContext, roomId: string, fields: RoomFields) {
  return request.put(`${apiBaseUrl}/rooms/${roomId}`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: fields,
  });
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-03's room-CRUD follow-up). */
export function deleteRoomResponse(request: APIRequestContext, roomId: string) {
  return request.delete(`${apiBaseUrl}/rooms/${roomId}`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-05). */
export function createTableGroupResponse(request: APIRequestContext, tableIds: string[]) {
  return request.post(`${apiBaseUrl}/table-groups`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { tableIds },
  });
}

export async function createTableGroup(request: APIRequestContext, tableIds: string[]): Promise<TableDto[]> {
  const response = await createTableGroupResponse(request, tableIds);
  if (!response.ok()) {
    throw new Error(`POST /table-groups failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-05). */
export function deleteTableGroupResponse(request: APIRequestContext, groupId: string) {
  return request.delete(`${apiBaseUrl}/table-groups/${groupId}`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
}

export async function deleteTableGroup(request: APIRequestContext, groupId: string): Promise<void> {
  const response = await deleteTableGroupResponse(request, groupId);
  if (!response.ok()) {
    throw new Error(`DELETE /table-groups/${groupId} failed: ${response.status()} ${await response.text()}`);
  }
}

/** Raw response so callers can assert on status/body for the failure cases too. */
export function openOrderResponse(request: APIRequestContext, tableId: string, coverCount: number) {
  return request.post(`${apiBaseUrl}/orders`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { tableId, coverCount },
  });
}

export async function openOrder(
  request: APIRequestContext,
  tableId: string,
  coverCount: number,
): Promise<OrderDto> {
  const response = await openOrderResponse(request, tableId, coverCount);
  if (!response.ok()) {
    throw new Error(`POST /orders failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Opens a takeaway/counter-sale order (ORD-20) — no Floor table involved, so no race to retry against. */
export async function openTakeawayOrder(request: APIRequestContext, label: string | null = null): Promise<OrderDto> {
  const response = await request.post(`${apiBaseUrl}/orders/takeaway`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { label },
  });
  if (!response.ok()) {
    throw new Error(`POST /orders/takeaway failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/**
 * Finds a free table and opens it, retrying against a different table on a
 * 409 (`floor.table_not_free`). Playwright runs specs across multiple
 * workers, so two sub-tests can both read the floor as "Mesa 5 is free" a
 * moment before either's own POST /orders lands — the API correctly lets
 * only one through and rejects the other. That rejection is the product
 * working as designed (docs/architecture/module-boundaries.md), not a bug
 * to work around by serializing tests; retrying with a fresh read is the
 * correct client behaviour on either side of that race.
 */
export async function openOrderOnAnyFreeTable(
  request: APIRequestContext,
  coverCount: number,
  attempts = 5,
): Promise<{ order: OrderDto; table: TableDto }> {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const table = findFreeTable(await getFloor(request));
    const response = await request.post(`${apiBaseUrl}/orders`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { tableId: table.id, coverCount },
    });

    if (response.ok()) {
      return { order: await response.json(), table };
    }

    if (response.status() !== 409 || attempt === attempts) {
      throw new Error(`POST /orders failed: ${response.status()} ${await response.text()}`);
    }
  }

  throw new Error('unreachable');
}

export async function getOrder(request: APIRequestContext, orderId: string): Promise<OrderDto> {
  const response = await request.get(`${apiBaseUrl}/orders/${orderId}`);
  if (!response.ok()) {
    throw new Error(`GET /orders/${orderId} failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export async function addLine(
  request: APIRequestContext,
  orderId: string,
  menuItemId: string,
  quantity: number,
  selectedModifierIds: string[] = [],
): Promise<OrderDto> {
  const response = await request.post(`${apiBaseUrl}/orders/${orderId}/lines`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { menuItemId, quantity, selectedModifierIds },
  });
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/lines failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-22). */
export function searchOrdersResponse(request: APIRequestContext, query: string) {
  return request.get(`${apiBaseUrl}/orders?${query}`);
}

export async function searchOrders(request: APIRequestContext, query: string): Promise<OrderSummaryDto[]> {
  const response = await searchOrdersResponse(request, query);
  if (!response.ok()) {
    throw new Error(`GET /orders?${query} failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-06). */
export function setLineNotesResponse(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  notes: string | null,
) {
  return request.put(`${apiBaseUrl}/orders/${orderId}/lines/${lineId}/notes`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { notes },
  });
}

export async function setLineNotes(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  notes: string | null,
): Promise<OrderDto> {
  const response = await setLineNotesResponse(request, orderId, lineId, notes);
  if (!response.ok()) {
    throw new Error(
      `PUT /orders/${orderId}/lines/${lineId}/notes failed: ${response.status()} ${await response.text()}`,
    );
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-03). */
export function setLineQuantityResponse(request: APIRequestContext, orderId: string, lineId: string, quantity: number) {
  return request.put(`${apiBaseUrl}/orders/${orderId}/lines/${lineId}/quantity`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { quantity },
  });
}

export async function setLineQuantity(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  quantity: number,
): Promise<OrderDto> {
  const response = await setLineQuantityResponse(request, orderId, lineId, quantity);
  if (!response.ok()) {
    throw new Error(
      `PUT /orders/${orderId}/lines/${lineId}/quantity failed: ${response.status()} ${await response.text()}`,
    );
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-11). `type`/`value` both null clears the discount. */
export function setLineDiscountResponse(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  type: string | null,
  value: number | null,
) {
  return request.put(`${apiBaseUrl}/orders/${orderId}/lines/${lineId}/discount`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { type, value },
  });
}

export async function setLineDiscount(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  type: string | null,
  value: number | null,
): Promise<OrderDto> {
  const response = await setLineDiscountResponse(request, orderId, lineId, type, value);
  if (!response.ok()) {
    throw new Error(
      `PUT /orders/${orderId}/lines/${lineId}/discount failed: ${response.status()} ${await response.text()}`,
    );
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-10). */
export function voidLineResponse(request: APIRequestContext, orderId: string, lineId: string, reason: string | null) {
  return request.post(`${apiBaseUrl}/orders/${orderId}/lines/${lineId}/void`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { reason },
  });
}

export async function voidLine(request: APIRequestContext, orderId: string, lineId: string, reason: string): Promise<OrderDto> {
  const response = await voidLineResponse(request, orderId, lineId, reason);
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/lines/${lineId}/void failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-11). `type`/`value` both null clears the discount. */
export function setOrderDiscountResponse(
  request: APIRequestContext,
  orderId: string,
  type: string | null,
  value: number | null,
) {
  return request.put(`${apiBaseUrl}/orders/${orderId}/discount`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { type, value },
  });
}

export async function setOrderDiscount(
  request: APIRequestContext,
  orderId: string,
  type: string | null,
  value: number | null,
): Promise<OrderDto> {
  const response = await setOrderDiscountResponse(request, orderId, type, value);
  if (!response.ok()) {
    throw new Error(`PUT /orders/${orderId}/discount failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-12). */
export function transferOrderResponse(request: APIRequestContext, orderId: string, newTableId: string) {
  return request.post(`${apiBaseUrl}/orders/${orderId}/transfer`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { newTableId },
  });
}

export async function transferOrder(request: APIRequestContext, orderId: string, newTableId: string): Promise<OrderDto> {
  const response = await transferOrderResponse(request, orderId, newTableId);
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/transfer failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/**
 * Picks a free table and transfers the order onto it, retrying against a
 * different table on a 409 (`floor.table_not_free`) — the same race
 * `openOrderOnAnyFreeTable` handles for initial seating, now possible here
 * too since another worker can occupy the picked table between reading the
 * floor and this request landing.
 */
export async function transferOrderToAnyFreeTable(
  request: APIRequestContext,
  orderId: string,
  attempts = 5,
): Promise<{ order: OrderDto; table: TableDto }> {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const table = findFreeTable(await getFloor(request));
    const response = await transferOrderResponse(request, orderId, table.id);

    if (response.ok()) {
      return { order: await response.json(), table };
    }

    if (response.status() !== 409 || attempt === attempts) {
      throw new Error(`POST /orders/${orderId}/transfer failed: ${response.status()} ${await response.text()}`);
    }
  }

  throw new Error('unreachable');
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-14). */
export function mergeOrdersResponse(request: APIRequestContext, primaryOrderId: string, secondaryOrderId: string) {
  return request.post(`${apiBaseUrl}/orders/${primaryOrderId}/merge`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { secondaryOrderId },
  });
}

export async function mergeOrders(
  request: APIRequestContext,
  primaryOrderId: string,
  secondaryOrderId: string,
): Promise<{ primaryOrder: OrderDto; secondaryOrder: OrderDto }> {
  const response = await mergeOrdersResponse(request, primaryOrderId, secondaryOrderId);
  if (!response.ok()) {
    throw new Error(`POST /orders/${primaryOrderId}/merge failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-13). */
export function transferLineResponse(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  destinationOrderId: string,
) {
  return request.post(`${apiBaseUrl}/orders/${orderId}/lines/${lineId}/transfer`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { destinationOrderId },
  });
}

export async function transferLine(
  request: APIRequestContext,
  orderId: string,
  lineId: string,
  destinationOrderId: string,
): Promise<{ sourceOrder: OrderDto; destinationOrder: OrderDto }> {
  const response = await transferLineResponse(request, orderId, lineId, destinationOrderId);
  if (!response.ok()) {
    throw new Error(
      `POST /orders/${orderId}/lines/${lineId}/transfer failed: ${response.status()} ${await response.text()}`,
    );
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-16). */
export function previewSplitByItemResponse(
  request: APIRequestContext,
  orderId: string,
  groups: { lines: { lineId: string; quantity: number }[] }[],
) {
  return request.post(`${apiBaseUrl}/orders/${orderId}/split/by-item`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { groups },
  });
}

export async function previewSplitByItem(
  request: APIRequestContext,
  orderId: string,
  groups: { lines: { lineId: string; quantity: number }[] }[],
): Promise<SplitByItemResponse> {
  const response = await previewSplitByItemResponse(request, orderId, groups);
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/split/by-item failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (ORD-18/19). */
export function getPreBillResponse(request: APIRequestContext, orderId: string) {
  return request.get(`${apiBaseUrl}/orders/${orderId}/pre-bill`);
}

export async function getPreBill(request: APIRequestContext, orderId: string): Promise<PreBillDto> {
  const response = await getPreBillResponse(request, orderId);
  if (!response.ok()) {
    throw new Error(`GET /orders/${orderId}/pre-bill failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (e.g. ORD-10's fully-voided-order case). */
export function closeOrderResponse(request: APIRequestContext, orderId: string) {
  return request.post(`${apiBaseUrl}/orders/${orderId}/close`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
}

/**
 * Closes an order and clears its table, returning the table to Free. Call
 * this at the end of any test that opened a table via the API — see the
 * file-level comment on why that matters for repeatability.
 */
/** Closes an order with no table to release — takeaway orders (ORD-20) only. Dine-in orders must use closeOrderAndClearTable. */
export async function closeOrder(request: APIRequestContext, orderId: string): Promise<CloseOrderResponse> {
  const response = await closeOrderResponse(request, orderId);
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/close failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export async function clearTable(request: APIRequestContext, tableId: string): Promise<void> {
  const clearResponse = await request.post(`${apiBaseUrl}/tables/${tableId}/clear`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
  if (!clearResponse.ok()) {
    throw new Error(`POST /tables/${tableId}/clear failed: ${clearResponse.status()} ${await clearResponse.text()}`);
  }
}

export async function closeOrderAndClearTable(
  request: APIRequestContext,
  orderId: string,
  tableId: string,
): Promise<void> {
  await closeOrder(request, orderId);
  await clearTable(request, tableId);
}

/** Raw response so callers can assert on status/body for the failure cases too (FLR-04). */
export function requestBillResponse(request: APIRequestContext, tableId: string) {
  return request.post(`${apiBaseUrl}/tables/${tableId}/request-bill`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
}

// Organization / Site / Terminal (IDN-01) — a narrow first slice, create and
// list only. No delete exists yet, so specs that create their own
// organization/site/terminal (rather than reusing the DevIdentitySeeder's
// seeded "Brasa Demo, Lda" / "Restaurante Central" / "Caixa 1") leave rows
// behind; harmless today since nothing asserts an exact count, only that a
// specific known row is present.

/** Raw response so callers can assert on status/body for the failure cases too (IDN-01). */
export function createOrganizationResponse(request: APIRequestContext, name: string) {
  return request.post(`${apiBaseUrl}/organizations`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { name },
  });
}

export async function createOrganization(request: APIRequestContext, name: string): Promise<OrganizationDto> {
  const response = await createOrganizationResponse(request, name);
  if (!response.ok()) {
    throw new Error(`POST /organizations failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export async function getOrganizations(request: APIRequestContext): Promise<OrganizationDto[]> {
  const response = await request.get(`${apiBaseUrl}/organizations`);
  if (!response.ok()) {
    throw new Error(`GET /organizations failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (IDN-01). */
export function createSiteResponse(request: APIRequestContext, organizationId: string, name: string, region: string) {
  return request.post(`${apiBaseUrl}/organizations/${organizationId}/sites`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { name, region },
  });
}

export async function createSite(
  request: APIRequestContext,
  organizationId: string,
  name: string,
  region: string,
): Promise<SiteDto> {
  const response = await createSiteResponse(request, organizationId, name, region);
  if (!response.ok()) {
    throw new Error(`POST /organizations/${organizationId}/sites failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export function getSitesResponse(request: APIRequestContext, organizationId: string) {
  return request.get(`${apiBaseUrl}/organizations/${organizationId}/sites`);
}

export async function getSites(request: APIRequestContext, organizationId: string): Promise<SiteDto[]> {
  const response = await getSitesResponse(request, organizationId);
  if (!response.ok()) {
    throw new Error(`GET /organizations/${organizationId}/sites failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (IDN-01). */
export function createTerminalResponse(request: APIRequestContext, siteId: string, label: string) {
  return request.post(`${apiBaseUrl}/sites/${siteId}/terminals`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { label },
  });
}

export async function createTerminal(request: APIRequestContext, siteId: string, label: string): Promise<TerminalDto> {
  const response = await createTerminalResponse(request, siteId, label);
  if (!response.ok()) {
    throw new Error(`POST /sites/${siteId}/terminals failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export function getTerminalsResponse(request: APIRequestContext, siteId: string) {
  return request.get(`${apiBaseUrl}/sites/${siteId}/terminals`);
}

export async function getTerminals(request: APIRequestContext, siteId: string): Promise<TerminalDto[]> {
  const response = await getTerminalsResponse(request, siteId);
  if (!response.ok()) {
    throw new Error(`GET /sites/${siteId}/terminals failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

// Price lists (CAT-05) — a narrow first slice, create/read/add-entry only.
// Nothing in AddLine or either web client resolves an effective price
// through these yet (no site-selection concept exists in pos/admin today);
// GetEffectivePrice is the resolution logic itself, exercised here directly
// against the API.

/** Raw response so callers can assert on status/body for the failure cases too (CAT-05). */
export function createPriceListResponse(request: APIRequestContext, siteId: string, name: string) {
  return request.post(`${apiBaseUrl}/price-lists`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { siteId, name },
  });
}

export async function createPriceList(request: APIRequestContext, siteId: string, name: string): Promise<PriceListDto> {
  const response = await createPriceListResponse(request, siteId, name);
  if (!response.ok()) {
    throw new Error(`POST /price-lists failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export function getPriceListResponse(request: APIRequestContext, priceListId: string) {
  return request.get(`${apiBaseUrl}/price-lists/${priceListId}`);
}

export async function getPriceList(request: APIRequestContext, priceListId: string): Promise<PriceListDto> {
  const response = await getPriceListResponse(request, priceListId);
  if (!response.ok()) {
    throw new Error(`GET /price-lists/${priceListId} failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export async function getPriceListsForSite(request: APIRequestContext, siteId: string): Promise<PriceListDto[]> {
  const response = await request.get(`${apiBaseUrl}/sites/${siteId}/price-lists`);
  if (!response.ok()) {
    throw new Error(`GET /sites/${siteId}/price-lists failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-05). */
export function addPriceListEntryResponse(request: APIRequestContext, priceListId: string, menuItemId: string, price: number) {
  return request.post(`${apiBaseUrl}/price-lists/${priceListId}/entries`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { menuItemId, price },
  });
}

export async function addPriceListEntry(
  request: APIRequestContext,
  priceListId: string,
  menuItemId: string,
  price: number,
): Promise<PriceListDto> {
  const response = await addPriceListEntryResponse(request, priceListId, menuItemId, price);
  if (!response.ok()) {
    throw new Error(`POST /price-lists/${priceListId}/entries failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export function getEffectivePriceResponse(request: APIRequestContext, priceListId: string, menuItemId: string) {
  return request.get(`${apiBaseUrl}/price-lists/${priceListId}/effective-price/${menuItemId}`);
}

export async function getEffectivePrice(
  request: APIRequestContext,
  priceListId: string,
  menuItemId: string,
): Promise<EffectivePriceDto> {
  const response = await getEffectivePriceResponse(request, priceListId, menuItemId);
  if (!response.ok()) {
    throw new Error(
      `GET /price-lists/${priceListId}/effective-price/${menuItemId} failed: ${response.status()} ${await response.text()}`,
    );
  }
  return response.json();
}

// Combos (CAT-10) — a narrow first slice, create/read/add-component only.
// Ringing one up is POST /orders/{id}/combo-lines, which decomposes into
// ordinary AddLine calls, one per component, so it needs no new order-side
// helper here — addComboLineResponse below just posts and returns the
// updated OrderDto the same way addLine already does.

/** Raw response so callers can assert on status/body for the failure cases too (CAT-10). */
export function createComboResponse(request: APIRequestContext, name: string, price: number) {
  return request.post(`${apiBaseUrl}/combos`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { name, price },
  });
}

export async function createCombo(request: APIRequestContext, name: string, price: number): Promise<ComboDto> {
  const response = await createComboResponse(request, name, price);
  if (!response.ok()) {
    throw new Error(`POST /combos failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export function getComboResponse(request: APIRequestContext, comboId: string) {
  return request.get(`${apiBaseUrl}/combos/${comboId}`);
}

export async function getCombo(request: APIRequestContext, comboId: string): Promise<ComboDto> {
  const response = await getComboResponse(request, comboId);
  if (!response.ok()) {
    throw new Error(`GET /combos/${comboId} failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-10). */
export function addComboComponentResponse(request: APIRequestContext, comboId: string, menuItemId: string) {
  return request.post(`${apiBaseUrl}/combos/${comboId}/components`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { menuItemId },
  });
}

export async function addComboComponent(request: APIRequestContext, comboId: string, menuItemId: string): Promise<ComboDto> {
  const response = await addComboComponentResponse(request, comboId, menuItemId);
  if (!response.ok()) {
    throw new Error(`POST /combos/${comboId}/components failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-10). */
export function addComboLineResponse(request: APIRequestContext, orderId: string, comboId: string) {
  return request.post(`${apiBaseUrl}/orders/${orderId}/combo-lines`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { comboId },
  });
}

export async function addComboLine(request: APIRequestContext, orderId: string, comboId: string): Promise<OrderDto> {
  const response = await addComboLineResponse(request, orderId, comboId);
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/combo-lines failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

// Tax rules (CAT-07/08) — a narrow first slice, create/list/resolve only,
// not yet wired into AddLine or the fiscal document builder.

export interface CreateTaxRuleFields {
  isAlcoholic: boolean;
  isTakeaway: boolean;
  region: string;
  vatRatePercent: number;
  effectiveFromUtc: string;
  effectiveToUtc?: string | null;
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-07). */
export function createTaxRuleResponse(request: APIRequestContext, fields: CreateTaxRuleFields) {
  return request.post(`${apiBaseUrl}/tax-rules`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: fields,
  });
}

export async function createTaxRule(request: APIRequestContext, fields: CreateTaxRuleFields): Promise<TaxRuleDto> {
  const response = await createTaxRuleResponse(request, fields);
  if (!response.ok()) {
    throw new Error(`POST /tax-rules failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

export async function getTaxRules(request: APIRequestContext): Promise<TaxRuleDto[]> {
  const response = await request.get(`${apiBaseUrl}/tax-rules`);
  if (!response.ok()) {
    throw new Error(`GET /tax-rules failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}

/** Raw response so callers can assert on status/body for the failure cases too (CAT-08). */
export function resolveTaxRuleResponse(request: APIRequestContext, query: string) {
  return request.get(`${apiBaseUrl}/tax-rules/resolve?${query}`);
}

export async function resolveTaxRule(request: APIRequestContext, query: string): Promise<ResolvedTaxRuleDto> {
  const response = await resolveTaxRuleResponse(request, query);
  if (!response.ok()) {
    throw new Error(`GET /tax-rules/resolve?${query} failed: ${response.status()} ${await response.text()}`);
  }
  return response.json();
}
