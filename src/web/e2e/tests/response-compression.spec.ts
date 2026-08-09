import { expect, test } from '@playwright/test';

// API-11 — docs/architecture/api-contract.md §9: "Compression on all
// responses." Assume a phone on cellular in a basement dining room.

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

test.describe('response compression', () => {
  test('a JSON response is served br-encoded when the client accepts it', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/menu`, {
      headers: { 'Accept-Encoding': 'br, gzip' },
    });
    expect(response.status()).toBe(200);
    // Playwright's fetch-based client transparently decompresses the body,
    // so this only inspects the header, not the raw bytes.
    expect(response.headers()['content-encoding']).toBe('br');
  });

  test('falls back to gzip when the client does not accept br', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/menu`, {
      headers: { 'Accept-Encoding': 'gzip' },
    });
    expect(response.status()).toBe(200);
    expect(response.headers()['content-encoding']).toBe('gzip');
  });

  test('a ProblemDetails error response is compressed too, not just success bodies', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/client-requirements`, {
      headers: { 'Accept-Encoding': 'gzip' },
    });
    expect(response.status()).toBe(400);
    expect(response.headers()['content-type']).toContain('application/problem+json');
    expect(response.headers()['content-encoding']).toBe('gzip');
  });
});
