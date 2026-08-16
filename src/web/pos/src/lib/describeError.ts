import { ApiError } from '../api/client';
import i18n from '../i18n/i18n';

/**
 * The server's ProblemDetails.title is always English — it's a developer-
 * facing string, not localized copy (docs/architecture/api-contract.md).
 * Showing it verbatim means Portuguese-speaking staff see English error
 * text regardless of the language toggle, so this looks up a real
 * translation by the stable error code first (error.code.<code> in
 * resources/{pt,en}.ts) and only falls back to the raw server message —
 * with the code shown alongside, for support purposes — when no
 * translation exists yet for that code. An untranslated code is never
 * silently hidden, just shown in English until someone adds it.
 *
 * Its own module, not exported from App.tsx — CashPayment (nested under
 * Receipt, which App.tsx itself renders) needs it too, and importing it
 * back out of App.tsx would be a circular import.
 */
export function describeError(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.code) {
      const key = `error.code.${err.code}`;
      if (i18n.exists(key)) {
        return i18n.t(key);
      }
      return `${err.message} (${err.code})`;
    }
    return err.message;
  }
  return err instanceof Error ? err.message : i18n.t('error.generic');
}
