import { useTranslation } from 'react-i18next';

/**
 * Shown in place of a blank screen when something throws during render
 * (OPS-14) — an honest "something broke, reload" message beats a silently
 * blank back-office with no way back except an unprompted manual refresh.
 * `Sentry.ErrorBoundary` (main.tsx) already reported the error before
 * rendering this.
 */
export function ErrorFallback() {
  const { t } = useTranslation();

  return (
    <div className="error-fallback" role="alert">
      <h1>{t('error.crashedTitle')}</h1>
      <p>{t('error.crashedBody')}</p>
      <button type="button" onClick={() => window.location.reload()}>
        {t('error.reload')}
      </button>
    </div>
  );
}
