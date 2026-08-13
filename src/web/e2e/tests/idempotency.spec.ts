import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  createRoom,
  findFreeTable,
  findMenuItem,
  getFloor,
  getMenu,
  searchOrders,
} from './support/api';
import type { CloseOrderResponse, OrderDto } from './support/types';

// QA-11 / API-05 — every mutation takes an Idempotency-Key (hard rule 7,
// docs/architecture/api-contract.md). A retried request must replay the
// original response verbatim, never repeat the side effect: a retried
// "close table" that issued a second fiscal document would be a
// certification failure (CLAUDE.md hard rule 3), not just a bug.

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

/** Opens an order on a free table using a caller-chosen Idempotency-Key, retrying on a genuine 409 (a different table lost the race) with a fresh key each retry. */
async function openOrderOnAnyFreeTableWithKey(
  request: import('@playwright/test').APIRequestContext,
  key: string,
  coverCount: number,
  attempts = 5,
): Promise<{ order: OrderDto; table: import('./support/types').TableDto }> {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const table = findFreeTable(await getFloor(request));
    const response = await request.post(`${apiBaseUrl}/orders`, {
      headers: { 'Idempotency-Key': attempt === 1 ? key : crypto.randomUUID() },
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

test.describe('idempotency replay', () => {
  test('POST /orders replayed with the same key returns the same order, never a duplicate', async ({ request }) => {
    const key = crypto.randomUUID();
    const { order: first, table } = await openOrderOnAnyFreeTableWithKey(request, key, 2);

    const replay1 = await request.post(`${apiBaseUrl}/orders`, {
      headers: { 'Idempotency-Key': key },
      data: { tableId: table.id, coverCount: 2 },
    });
    const replay2 = await request.post(`${apiBaseUrl}/orders`, {
      headers: { 'Idempotency-Key': key },
      data: { tableId: table.id, coverCount: 2 },
    });

    expect(replay1.status()).toBe(201);
    expect(replay2.status()).toBe(201);
    expect(replay1.headers()['idempotent-replay']).toBe('true');
    expect(replay2.headers()['idempotent-replay']).toBe('true');
    expect((await replay1.json()).id).toBe(first.id);
    expect((await replay2.json()).id).toBe(first.id);

    // The replays never re-ran OpenOrderAsync's side effect — only one
    // open order exists for this table, not three.
    const openOrdersForTable = await searchOrders(request, `status=Open&tableId=${table.id}`);
    expect(openOrdersForTable).toHaveLength(1);

    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, first.id, item.id, 1);
    await closeOrderAndClearTable(request, first.id, table.id);
  });

  test('POST /orders/{id}/close replayed with the same key never issues a second fiscal document', async ({ request }) => {
    const { order, table } = await openOrderOnAnyFreeTableWithKey(request, crypto.randomUUID(), 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 1);

    const closeKey = crypto.randomUUID();
    const closeResponses: CloseOrderResponse[] = [];
    for (let attempt = 0; attempt < 3; attempt += 1) {
      const response = await request.post(`${apiBaseUrl}/orders/${order.id}/close`, {
        headers: { 'Idempotency-Key': closeKey },
      });
      expect(response.status()).toBe(200);
      if (attempt > 0) {
        expect(response.headers()['idempotent-replay']).toBe('true');
      }
      closeResponses.push(await response.json());
    }

    // The critical fiscal invariant: the exact same document on every
    // replay, not a second one issued behind a retried close.
    expect(closeResponses[1].document.documentNumber).toBe(closeResponses[0].document.documentNumber);
    expect(closeResponses[2].document.documentNumber).toBe(closeResponses[0].document.documentNumber);
    expect(closeResponses[1].document.atcud).toBe(closeResponses[0].document.atcud);

    await request.post(`${apiBaseUrl}/tables/${table.id}/clear`, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    });
  });

  test('a different Idempotency-Key against an already-occupied table is a genuine conflict, not a cache hit', async ({
    request,
  }) => {
    const { order, table } = await openOrderOnAnyFreeTableWithKey(request, crypto.randomUUID(), 2);

    const second = await request.post(`${apiBaseUrl}/orders`, {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      data: { tableId: table.id, coverCount: 2 },
    });
    expect(second.status()).toBe(409);
    expect(second.headers()['idempotent-replay']).toBeUndefined();

    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');
    await addLine(request, order.id, item.id, 1);
    await closeOrderAndClearTable(request, order.id, table.id);
  });

  test('a mutating request with no Idempotency-Key is rejected', async ({ request }) => {
    const table = findFreeTable(await getFloor(request));
    const response = await request.post(`${apiBaseUrl}/orders`, {
      data: { tableId: table.id, coverCount: 2 },
    });
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('request.idempotency_key_required');
  });

  // Regression for a real bug: IdempotencyMiddleware used to call
  // WriteAsync unconditionally on both the cache-and-forward path and the
  // replay path, including for an empty (204) body. Kestrel rejects any
  // body write at all on a response that forbids one, which threw inside
  // the middleware and crashed the whole connection -- not just this
  // request, every in-flight request on the API process. A DELETE (the
  // only 204-returning verb here) replayed with the same key exercises
  // both the original and the cached path; the API must still answer a
  // plain request afterward, not have taken the process down with it.
  test('a replayed DELETE (204) does not crash the API', async ({ request }) => {
    const room = await createRoom(request, { name: `Idempotency 204 Test ${Date.now()}`, displayOrder: 99 });
    const deleteKey = crypto.randomUUID();

    const first = await request.delete(`${apiBaseUrl}/rooms/${room.id}`, {
      headers: { 'Idempotency-Key': deleteKey },
    });
    expect(first.status()).toBe(204);
    expect(first.headers()['idempotent-replay']).toBeUndefined();

    const replay = await request.delete(`${apiBaseUrl}/rooms/${room.id}`, {
      headers: { 'Idempotency-Key': deleteKey },
    });
    expect(replay.status()).toBe(204);
    expect(replay.headers()['idempotent-replay']).toBe('true');

    const stillUp = await getFloor(request);
    expect(Array.isArray(stillUp)).toBe(true);
  });
});
