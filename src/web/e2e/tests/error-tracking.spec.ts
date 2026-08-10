import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// OPS-14 — client-side error tracking. Sentry.init() runs unconditionally
// with no dsn configured (empty by default, nothing committed — same
// "ship the seam, no real collector yet" shape as OPS-08's OpenTelemetry),
// so it never sends anything anywhere, but it still wires up automatic
// window.onerror/unhandledrejection capture and the render-tree
// ErrorBoundary. window.__errorReportingInitialized is this app's own
// signal that Sentry.init() completed without throwing — not a
// Sentry-internal implementation detail relied on as a proxy for it.
//
// Sentry.ErrorBoundary itself is verified against a genuine thrown error,
// not just "the component exists in the tree": DevCrashTrigger throws for
// real when `?__crashTest=1` is present, but only in dev builds
// (import.meta.env.DEV is a literal `false` in production, so the whole
// branch is dead-code-eliminated — verified by grepping the production
// bundle, see the component's own doc comment).

test.describe('client-side error tracking (OPS-14)', () => {
  test('pos initialises error reporting on a normal load', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toBeVisible();
    expect(await page.evaluate(() => window.__errorReportingInitialized)).toBe(true);
  });

  test('pos catches a render-phase error and shows the fallback, not a blank screen', async ({ page }) => {
    await page.goto('/?__crashTest=1');

    const fallback = page.getByRole('alert');
    await expect(fallback).toBeVisible();
    await expect(fallback.getByRole('heading')).toHaveText('Ocorreu um problema');
    await expect(fallback.getByRole('button', { name: 'Recarregar' })).toBeVisible();

    // The crashed tree never mounted — none of the real app's chrome exists.
    await expect(page.getByRole('heading', { name: 'Escolher mesa' })).toHaveCount(0);

    // Same QA-14 bar as every other screen — arguably more important here,
    // since a user seeing this one is already having a bad time.
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });

  test('admin initialises error reporting on a normal load', async ({ page }) => {
    await page.goto(adminBaseUrl);
    await expect(page.getByRole('heading', { name: 'Visão geral' })).toBeVisible();
    expect(await page.evaluate(() => window.__errorReportingInitialized)).toBe(true);
  });

  test('admin catches a render-phase error and shows the fallback, not a blank screen', async ({ page }) => {
    await page.goto(`${adminBaseUrl}/?__crashTest=1`);

    const fallback = page.getByRole('alert');
    await expect(fallback).toBeVisible();
    await expect(fallback.getByRole('heading')).toHaveText('Ocorreu um problema');
    await expect(fallback.getByRole('button', { name: 'Recarregar' })).toBeVisible();

    await expect(page.getByRole('heading', { name: 'Visão geral' })).toHaveCount(0);

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
