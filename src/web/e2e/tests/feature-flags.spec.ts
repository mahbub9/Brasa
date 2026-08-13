import { expect, test } from '@playwright/test';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';
const apiBaseUrl = (process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216') + '/api/v1';

// IDN-16 — a per-tenant, optionally per-platform on/off switch. No real
// consumer exists yet (no native app, no OAuth/tiering story to key a
// paid feature off of) — this proves the mechanism itself: a
// platform-specific override wins over the tenant's "all platforms" row,
// an unconfigured flag defaults to off, and a replayed set is idempotent
// like every other mutation here. Every key in this spec is unique per
// test (Date.now()-suffixed) so parallel specs and reruns never collide
// on shared flag state.

interface FeatureFlagDto {
  id: string;
  key: string;
  platform: string;
  isEnabled: boolean;
}

function idempotencyKey() {
  return crypto.randomUUID();
}

test.describe('feature flags (IDN-16)', () => {
  test('sets a flag with no platform, defaulting to "all", and lists it', async ({ request }) => {
    const key = `flag-all-${Date.now()}`;

    const setResponse = await request.put(`${apiBaseUrl}/feature-flags/${key}`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { isEnabled: true },
    });
    expect(setResponse.status()).toBe(200);
    const set: FeatureFlagDto = await setResponse.json();
    expect(set.key).toBe(key);
    expect(set.platform).toBe('all');
    expect(set.isEnabled).toBe(true);

    const listResponse = await request.get(`${apiBaseUrl}/feature-flags`);
    const flags: FeatureFlagDto[] = await listResponse.json();
    expect(flags.some((f) => f.key === key && f.platform === 'all' && f.isEnabled)).toBe(true);
  });

  test('a platform-specific override wins over the tenant\'s "all platforms" row on resolve', async ({ request }) => {
    const key = `flag-override-${Date.now()}`;

    await request.put(`${apiBaseUrl}/feature-flags/${key}`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { isEnabled: true },
    });
    await request.put(`${apiBaseUrl}/feature-flags/${key}`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { platform: 'ios', isEnabled: false },
    });

    const resolvedWeb = await request.get(`${apiBaseUrl}/feature-flags/${key}/resolve?platform=web`);
    expect((await resolvedWeb.json()) as FeatureFlagDto).toMatchObject({ key, platform: 'web', isEnabled: true });

    const resolvedIos = await request.get(`${apiBaseUrl}/feature-flags/${key}/resolve?platform=ios`);
    expect((await resolvedIos.json()) as FeatureFlagDto).toMatchObject({ key, platform: 'ios', isEnabled: false });
  });

  test('resolving a key nobody has ever configured defaults to disabled, not enabled', async ({ request }) => {
    const key = `flag-unconfigured-${Date.now()}`;

    const response = await request.get(`${apiBaseUrl}/feature-flags/${key}/resolve`);
    expect(response.status()).toBe(200);
    expect((await response.json()) as FeatureFlagDto).toMatchObject({ key, platform: 'all', isEnabled: false });
  });

  test('re-setting the same key/platform updates in place rather than duplicating the row', async ({ request }) => {
    const key = `flag-toggle-${Date.now()}`;

    await request.put(`${apiBaseUrl}/feature-flags/${key}`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { isEnabled: true },
    });
    await request.put(`${apiBaseUrl}/feature-flags/${key}`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { isEnabled: false },
    });

    const flags: FeatureFlagDto[] = await (await request.get(`${apiBaseUrl}/feature-flags`)).json();
    const matching = flags.filter((f) => f.key === key);
    expect(matching).toHaveLength(1);
    expect(matching[0].isEnabled).toBe(false);
  });

  test('rejects an empty key', async ({ request }) => {
    const response = await request.put(`${apiBaseUrl}/feature-flags/${encodeURIComponent(' ')}`, {
      headers: { 'Idempotency-Key': idempotencyKey() },
      data: { isEnabled: true },
    });
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('identity.invalid_feature_flag_key');
  });

  test('the admin editor adds a flag and toggles it off', async ({ page }) => {
    const key = `flag-ui-${Date.now()}`;

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-flags').click();
    await page.waitForSelector('.feature-flag-manager');

    await page.getByTestId('add-flag').click();
    await page.getByTestId('new-flag-key').fill(key);
    await page.getByTestId('new-flag-save').click();

    // Admin defaults to Portuguese (same brasa.lang cookie convention as pos).
    const row = page.getByTestId(`flag-${key}-all`);
    await expect(row).toBeVisible();
    await expect(row).toContainText('Ativa');

    await row.getByTestId(`flag-toggle-${key}-all`).click();
    await expect(row).toContainText('Inativa');
  });
});
