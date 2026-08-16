import { expect, test } from '@playwright/test';
import { closeOrderAndClearTable, findMenuItem, getMenu, getOrder, openOrderOnAnyFreeTable } from './support/api';
import type { OrderDto } from './support/types';

// ORD-21 / QA-08 — two terminals racing to mutate the same order used to be
// a silent last-write-wins overwrite: Order carried no concurrency token at
// all, so the second SaveChangesAsync just blindly clobbered the first with
// no error and no trace either change was ever lost. Order now carries the
// same xmin token Table already had (TableConfiguration.cs), and every
// order-mutating endpoint reports a lost race as a real 409 instead
// (OrderEndpoints.TrySaveOrderAsync).
//
// This does NOT try to force the actual conflict to happen over real HTTP —
// tried that first (8, then 40 genuinely concurrent AddLine requests against
// the same order, `Promise.all`) and it never once triggered here: this
// machine's local Kestrel/Postgres round trip is fast enough that even 40
// concurrent requests didn't overlap at the SaveChangesAsync level. Forcing
// a specific interleaving from outside a real HTTP client isn't reliably
// possible without a test-only seam this codebase doesn't have (and
// shouldn't grow just for this), so this spec asserts what IS true
// regardless of whether the race actually manifests on a given run: no
// lost update, no corrupted state, no unrelated failures, and IF a conflict
// does occur, it is always the correct, well-formed code. The mechanism
// itself — that a genuinely stale write really does throw
// DbUpdateConcurrencyException and really does get caught — is proven
// deterministically instead, with no timing dependence at all, by directly
// controlling two DbContexts in
// Brasa.Api.IntegrationTests/OrderConcurrencyIntegrationTests.cs.

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

test.describe('order concurrency (ORD-21)', () => {
  test('several concurrent terminals adding a line to the same order never lose or duplicate a line', async ({
    request,
  }) => {
    const { order } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');

    const concurrentRequests = 8;
    const responses = await Promise.all(
      Array.from({ length: concurrentRequests }, () =>
        request.post(`${apiBaseUrl}/orders/${order.id}/lines`, {
          headers: { 'Idempotency-Key': crypto.randomUUID() },
          data: { menuItemId: item.id, quantity: 1, selectedModifierIds: [] },
        }),
      ),
    );

    const succeeded = responses.filter((r) => r.status() === 200);
    const conflicted = responses.filter((r) => r.status() === 409);

    // Every response is one or the other — nothing silently vanished, and
    // nothing failed for an unrelated reason.
    expect(succeeded.length + conflicted.length).toBe(concurrentRequests);

    // Whenever the race genuinely does land (see the file-level comment —
    // not guaranteed on every run), the loser is always this exact code,
    // never a raw 500 or some other error swallowing it.
    for (const response of conflicted) {
      const body = await response.json();
      expect(body.code).toBe('order.concurrently_modified');
    }

    // No lost update, whether or not any request actually conflicted: the
    // order ends up with exactly one line per successful response, never
    // fewer (a silently dropped winner — the exact bug ORD-21 fixes) and
    // never more (a phantom line from a "failed" request that actually
    // committed).
    const finalOrder: OrderDto = await getOrder(request, order.id);
    expect(finalOrder.lines).toHaveLength(succeeded.length);

    await closeOrderAndClearTable(request, order.id, order.tableId);
  });

  test('a rejected concurrent mutation names the order conflict, not some other error', async ({ request }) => {
    const { order } = await openOrderOnAnyFreeTable(request, 2);
    const item = findMenuItem(await getMenu(request), 'Pão e Azeitonas');

    const [first, second] = await Promise.all([
      request.post(`${apiBaseUrl}/orders/${order.id}/lines`, {
        headers: { 'Idempotency-Key': crypto.randomUUID() },
        data: { menuItemId: item.id, quantity: 1, selectedModifierIds: [] },
      }),
      request.post(`${apiBaseUrl}/orders/${order.id}/lines`, {
        headers: { 'Idempotency-Key': crypto.randomUUID() },
        data: { menuItemId: item.id, quantity: 2, selectedModifierIds: [] },
      }),
    ]);

    for (const response of [first, second]) {
      expect([200, 409]).toContain(response.status());
      if (response.status() === 409) {
        const body = await response.json();
        expect(body.code).toBe('order.concurrently_modified');
        expect(typeof body.title).toBe('string');
      }
    }

    await closeOrderAndClearTable(request, order.id, order.tableId);
  });
});
