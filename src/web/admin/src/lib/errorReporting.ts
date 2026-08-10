import * as Sentry from '@sentry/react';

/**
 * Client-side error tracking (OPS-14) — the same "ship the seam ahead of
 * the trigger" shape as OPS-08's OpenTelemetry wiring: `Sentry.init()` runs
 * unconditionally, but with no `dsn` configured (empty by default, nothing
 * committed) it never sends anything anywhere — a documented Sentry SDK
 * behaviour, not a workaround. Global `window.onerror`/`unhandledrejection`
 * capture comes for free from Sentry's own default integrations; only the
 * React render-tree boundary (`ErrorBoundary` in main.tsx) is this file's
 * own responsibility to wire up.
 *
 * `window.__errorReportingInitialized` is set on success so `admin`'s own
 * E2E suite can confirm initialisation actually completed, without relying
 * on any Sentry-internal global as a proxy for it.
 */
export function initErrorReporting(): void {
  Sentry.init({
    dsn: import.meta.env.VITE_SENTRY_DSN || undefined,
    environment: import.meta.env.MODE,
  });

  window.__errorReportingInitialized = true;
}

declare global {
  interface Window {
    __errorReportingInitialized?: boolean;
  }
}
