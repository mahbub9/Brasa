import type { APIRequestContext } from '@playwright/test';
import type { MenuCategoryDto, OrderDto, RoomDto, TableDto } from './types';

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

export async function addLine(
  request: APIRequestContext,
  orderId: string,
  menuItemId: string,
  quantity: number,
): Promise<OrderDto> {
  const response = await request.post(`${apiBaseUrl}/orders/${orderId}/lines`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { menuItemId, quantity },
  });
  if (!response.ok()) {
    throw new Error(`POST /orders/${orderId}/lines failed: ${response.status()} ${await response.text()}`);
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
