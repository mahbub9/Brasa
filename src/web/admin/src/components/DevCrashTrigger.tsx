/**
 * Dev-only hook so the E2E suite can prove `Sentry.ErrorBoundary`
 * (main.tsx) actually catches a render-phase error and shows
 * `ErrorFallback`, rather than only testing that the boundary is present
 * in the tree. `import.meta.env.DEV` is replaced with a literal `false` in
 * a production build (Vite's own documented behaviour), so the `throw`
 * below is dead code there — never a code path a real user can reach, not
 * even by guessing the query param. See OPS-14 / error-tracking.spec.ts.
 */
export function DevCrashTrigger() {
  if (import.meta.env.DEV && new URLSearchParams(window.location.search).get('__crashTest') === '1') {
    throw new Error('Deliberate test crash (OPS-14 E2E verification) — not a real bug.');
  }

  return null;
}
