import { expect, test } from '@playwright/test';

// API-06/07 — X-Brasa-Client header parsing and GET /client-requirements
// (docs/architecture/api-contract.md §3). Ships ahead of any client that
// actually sends the header or checks this yet, the same way CAT-02/CAT-18
// shipped ahead of the admin UI that will eventually call them.

const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

test.describe('client version negotiation', () => {
  test('a known client id returns its configured version policy', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/client-requirements`, {
      headers: { 'X-Brasa-Client': 'pos-web/0.0.0 (web)' },
    });
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body).toEqual({ minimumSupported: '0.0.0', recommended: '0.0.0', sunsetAfter: null });
  });

  test('a missing X-Brasa-Client header is rejected', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/client-requirements`);
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('client.header_required');
  });

  test('a malformed X-Brasa-Client header is rejected the same as a missing one', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/client-requirements`, {
      headers: { 'X-Brasa-Client': 'not-a-valid-header-value' },
    });
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('client.header_required');
  });

  test('a well-formed header naming an unconfigured client id 404s', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/client-requirements`, {
      headers: { 'X-Brasa-Client': 'kds-terminal/1.0.0 (android)' },
    });
    expect(response.status()).toBe(404);
    expect((await response.json()).code).toBe('client.unknown_client_id');
  });
});
