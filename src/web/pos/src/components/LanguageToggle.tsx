import { useTranslation } from 'react-i18next';
import type { SupportedLanguage } from '../i18n/languageStorage';

const LANGUAGES: SupportedLanguage[] = ['pt', 'en'];

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
