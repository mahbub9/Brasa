import { expect, test } from '@playwright/test';
import { testClockHeader } from './support/api';

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

// QA-04 — a per-request IClock override, dev/test-only, never reachable in
// Production (TestClockMiddleware throws on the very first request if
// IsProduction() is true — see its own remarks). Proven directly against
// GET /ping, which echoes IClock.UtcNow verbatim, independent of any
// specific feature's own business logic. menu-item-scheduled-price.spec.ts
// (CAT-16) is the real first consumer, replacing a real ~2s wait with this.

test.describe('test clock override (QA-04)', () => {
  test('X-Brasa-Test-Clock overrides IClock.UtcNow for that request only', async ({ request }) => {
    const fixedInstant = '2030-01-01T00:00:00.000Z';
    const overridden = await request.get(`${apiBaseUrl}/ping`, { headers: testClockHeader(fixedInstant) });
    expect(overridden.ok()).toBe(true);
    const overriddenBody = await overridden.json();
    // The server renders "O" format (7 fractional digits, explicit offset),
    // not necessarily byte-identical to the request's own ISO string -- the
    // instant is what must match, not the exact rendering.
    expect(new Date(overriddenBody.utc as string).getTime()).toBe(new Date(fixedInstant).getTime());

    // A request with no header still sees the real clock -- confirms the
    // override is scoped to its own request, not a global mutation that
    // would leak into every other concurrent request.
    const real = await request.get(`${apiBaseUrl}/ping`);
    const realBody = await real.json();
    expect(realBody.utc).not.toBe(fixedInstant);
    expect(new Date(realBody.utc as string).getFullYear()).toBeLessThan(2030);
  });

  test('an unparseable header is silently ignored, the real clock still answers', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/ping`, { headers: testClockHeader('not-a-date') });
    expect(response.ok()).toBe(true);
    const body = await response.json();
    expect(new Date(body.utc as string).getFullYear()).toBeLessThan(2030);
  });
});
