import type { APIRequestContext } from '@playwright/test';
import type { MenuCategoryDto, OrderDto } from './types';

// Deterministic test-data builders (QA-03) — set up state via the API
// directly instead of clicking through the UI, so specs that aren't
// exercising the ordering flow itself don't pay for it. The happy-path spec
// (walking-skeleton.spec.ts) deliberately does NOT use these — it drives the
// real UI end to end, because that is the one thing worth proving by hand.

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

export async function openOrder(
  request: APIRequestContext,
  tableLabel: string,
  coverCount: number,
): Promise<OrderDto> {
  const response = await request.post(`${apiBaseUrl}/orders`, {
    headers: { 'Idempotency-Key': idempotencyKey() },
    data: { tableLabel, coverCount },
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
