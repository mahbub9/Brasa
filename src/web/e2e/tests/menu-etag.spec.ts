import type { APIResponse } from '@playwright/test';
import { expect, test } from '@playwright/test';

// API-10 — GET /menu returns a strong ETag; a repeat request that already
// has that value should come back as a bodyless 304, not the same JSON
// again. See docs/architecture/api-contract.md §9.

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

test.describe('menu ETag caching', () => {
  test('returns 200 with an ETag, then 304 when it is echoed back as If-None-Match', async ({ request }) => {
    // Several sibling specs (CAT-01/13/19) mutate catalog state that
    // changes GET /menu's body — and therefore its ETag — as part of what
    // they're testing (category visibility, item availability, price).
    // Under real parallel workers, one of those can land in the gap
    // between this test's two calls, which is a genuine 200 (the menu
    // really did change), not a broken ETag mechanism. Retrying the whole
    // round trip a few times proves the real invariant (an unchanged ETag
    // 304s) without being flaky about timing it can't control.
    let second: APIResponse | undefined;
    let etag = '';
    for (let attempt = 1; attempt <= 5; attempt += 1) {
      const first = await request.get(`${apiBaseUrl}/menu`);
      expect(first.status()).toBe(200);
      etag = first.headers()['etag'];
      expect(etag).toBeTruthy();
      expect((await first.body()).length).toBeGreaterThan(0);

      second = await request.get(`${apiBaseUrl}/menu`, {
        headers: { 'If-None-Match': etag },
      });

      if (second.status() === 304) break;
    }

    expect(second?.status()).toBe(304);
    expect((await second?.body())?.length).toBe(0);
    // A 304 still restates the ETag, so a client that dropped its cached
    // copy can tell the response matches without re-requesting.
    expect(second?.headers()['etag']).toBe(etag);
  });

  test('an unrecognised If-None-Match value still gets the full body', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/menu`, {
      headers: { 'If-None-Match': '"not-a-real-etag"' },
    });
    expect(response.status()).toBe(200);
    expect((await response.body()).length).toBeGreaterThan(0);
  });
});
