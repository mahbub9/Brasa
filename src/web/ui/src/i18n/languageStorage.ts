export type SupportedLanguage = 'pt' | 'en';

/**
 * Where the chosen UI language is persisted (WEB-02, shared by `pos` and
 * `admin`).
 *
 * This is the seam for mobile: a React Native client implements the same
 * shape backed by AsyncStorage/SecureStore instead of document.cookie, and
 * nothing else -- i18n.ts, the translation resources, every component --
 * has to change. Keep language *storage* and language *configuration*
 * (each app's own i18n.ts) separate for exactly this reason.
 *
 * Deliberately the same cookie name across every web client (`brasa.lang`,
 * no `Domain` attribute, so scoped to the host but not the port -- shared
 * across `pos` and `admin` on `localhost` in dev). A staff member's
 * language choice is a preference for *them*, not for whichever client app
 * they happened to open first.
 */
export interface LanguageStore {
  get(): SupportedLanguage | null;
  set(language: SupportedLanguage): void;
}

const COOKIE_NAME = 'brasa.lang';
const COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 365;

function isSupportedLanguage(value: string): value is SupportedLanguage {
  return value === 'pt' || value === 'en';
}

/**
 * Web implementation. This is a UI-preference cookie, not an auth cookie --
 * it is never sent to or read by the API (nothing attaches it to a fetch,
 * and the API has no code path that reads cookies at all). It does not
 * conflict with ADR 0008 (token auth, no cookies): that ADR is about
 * authentication, not about where a browser happens to keep a client-only
 * rendering preference.
 */
export const cookieLanguageStore: LanguageStore = {
  get() {
    const match = document.cookie.match(new RegExp(`(?:^|; )${COOKIE_NAME}=([^;]*)`));
    const value = match ? decodeURIComponent(match[1]) : '';
    return isSupportedLanguage(value) ? value : null;
  },

  set(language) {
    const secure = window.location.protocol === 'https:' ? '; Secure' : '';
    document.cookie = `${COOKIE_NAME}=${language}; Max-Age=${COOKIE_MAX_AGE_SECONDS}; Path=/; SameSite=Lax${secure}`;
  },
};
