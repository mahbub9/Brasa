import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import { cookieLanguageStore, type SupportedLanguage } from '@brasa/ui/i18n/languageStorage';
import { en } from './resources/en';
import { pt } from './resources/pt';

// Portugal is the primary market — pt is the default, en is the toggle, not
// the other way round. See docs/architecture/decisions/0011-i18n.md.
const DEFAULT_LANGUAGE: SupportedLanguage = 'pt';

const initialLanguage = cookieLanguageStore.get() ?? DEFAULT_LANGUAGE;

void i18next.use(initReactI18next).init({
  resources: {
    pt: { translation: pt },
    en: { translation: en },
  },
  lng: initialLanguage,
  fallbackLng: DEFAULT_LANGUAGE,
  interpolation: { escapeValue: false }, // React already escapes.
});

document.documentElement.lang = initialLanguage;

i18next.on('languageChanged', (lng) => {
  if (lng === 'pt' || lng === 'en') {
    cookieLanguageStore.set(lng);
    document.documentElement.lang = lng;
  }
});

export default i18next;
