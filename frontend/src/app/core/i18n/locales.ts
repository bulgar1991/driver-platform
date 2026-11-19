/**
 * Languages of the public site. Each is a separate build (Angular i18n, see angular.json): the main
 * language at the root, the others under their prefix. The main language is set per server
 * (SITE_LANGUAGE in deploy/.env → `ng build --define SITE_LANGUAGE`); English when not set.
 * Translations: src/locale/messages.<code>.json.
 */
export interface SiteLocale {
  code: 'it' | 'en' | 'de' | 'fr'
  /** In its own language, for the switcher. */
  name: string
  /** URL prefix: '' for the main language, else '/it', '/en', '/de' or '/fr'. */
  prefix: string
  /** Open Graph locale. */
  og: string
}

/** In the order of the language switch: English, French, German, Italian. */
const LANGUAGES: readonly Omit<SiteLocale, 'prefix'>[] = [
  { code: 'en', name: 'English', og: 'en_GB' },
  { code: 'fr', name: 'Français', og: 'fr_FR' },
  { code: 'de', name: 'Deutsch', og: 'de_DE' },
  { code: 'it', name: 'Italiano', og: 'it_IT' },
]

/** The language at the root of the site. */
export const MAIN_LANGUAGE: SiteLocale['code'] =
  typeof SITE_LANGUAGE === 'string' &&
  LANGUAGES.some(l => l.code === SITE_LANGUAGE)
    ? (SITE_LANGUAGE as SiteLocale['code'])
    : 'en'

export const SITE_LOCALES: readonly SiteLocale[] = LANGUAGES.map(l => ({
  ...l,
  prefix: l.code === MAIN_LANGUAGE ? '' : `/${l.code}`,
}))

export const DEFAULT_LOCALE = SITE_LOCALES.find(l => l.code === MAIN_LANGUAGE)!

/** The site language for an Angular LOCALE_ID ("de", "en-US"…); the main language when unknown. */
export function siteLocale(localeId: string): SiteLocale {
  const code = localeId.slice(0, 2).toLowerCase()
  return SITE_LOCALES.find(l => l.code === code) ?? DEFAULT_LOCALE
}

const LOCALE_PREFIX = /^\/(?:it|en|de|fr)(?=\/|\?|#|$)/

/**
 * "/de/fr/tariffs?x=1" → "/tariffs?x=1". The router's URL normally has no prefix (each language is
 * its own build under its base href), but a build that gets another language's URL (the Italian
 * dev server opened at /de/…) sees it, and the prefixes must not pile up.
 */
export function stripLocalePrefix(url: string): string {
  let path = url
  while (LOCALE_PREFIX.test(path)) {
    path = path.replace(LOCALE_PREFIX, '')
  }
  return path.startsWith('/') ? path : `/${path}`
}
