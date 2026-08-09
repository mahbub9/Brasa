import type { APIRequestContext } from '@playwright/test';
import type { MenuCategoryDto, MenuItemDto, OrderDto, PreBillDto, RoomDto, TableDto } from './types';

// Deterministic test-data builders (QA-03) — set up state via the API
// directly instead of clicking through the UI, so specs that aren't
// exercising the ordering flow itself don't pay for it. The happy-path spec
// (walking-skeleton.spec.ts) deliberately does NOT use these — it drives the
// real UI end to end, because that is the one thing worth proving by hand.
//
// The dev database is NOT reset between runs (QA-02's known limitation —
// see docs/development/e2e-testing.md), and there are only 8 seeded tables.
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

export async function openOrder(
  request: APIRequestContext,
  tableId: string,
  coverCount: number,
): Promise<OrderDto> {
  const response = await request.post(`${apiBaseUrl}/orders`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { tableId, coverCount },
  });
  if (!response.ok()) {
    throw new Error(`POST /orders failed: ${response.status()} ${await response.text()}`);
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

/**
 * Closes an order and clears its table, returning the table to Free. Call
 * this at the end of any test that opened a table via the API — see the
 * file-level comment on why that matters for repeatability.
 */
export async function closeOrderAndClearTable(
  request: APIRequestContext,
  orderId: string,
  tableId: string,
): Promise<void> {
  const closeResponse = await request.post(`${apiBaseUrl}/orders/${orderId}/close`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
  if (!closeResponse.ok()) {
    throw new Error(`POST /orders/${orderId}/close failed: ${closeResponse.status()} ${await closeResponse.text()}`);
  }

  const clearResponse = await request.post(`${apiBaseUrl}/tables/${tableId}/clear`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
  });
  if (!clearResponse.ok()) {
    throw new Error(`POST /tables/${tableId}/clear failed: ${clearResponse.status()} ${await clearResponse.text()}`);
  }
}
