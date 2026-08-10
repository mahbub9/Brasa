import { useTranslation } from 'react-i18next';
import type { SupportedLanguage } from '../i18n/languageStorage';

const LANGUAGES: SupportedLanguage[] = ['pt', 'en'];

/**
 * The pt/en toggle shared by every web client (WEB-02, ADR 0011). Reads and
 * writes through whichever i18next instance is active in the rendering
 * app -- each app still configures its own `i18n.ts` (translation
 * resources are genuinely different per app), this component only needs
 * the hook, not the config.
 */
export function LanguageToggle() {
  const { t, i18n } = useTranslation();
  const current = i18n.resolvedLanguage ?? i18n.language;

  return (
    <div className="language-toggle" role="group" aria-label={t('language.label')}>
      {LANGUAGES.map((lang) => (
        <button
          key={lang}
          type="button"
          data-testid={`lang-${lang}`}
          aria-pressed={current === lang}
          className={current === lang ? 'active' : undefined}
          onClick={() => void i18n.changeLanguage(lang)}
        >
          {lang.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
