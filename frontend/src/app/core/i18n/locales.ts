/**
 * Languages of the public site. Each is a separate build (Angular i18n, see angular.json): Italian
 * at the root, the others under their prefix. Translations: src/locale/messages.<code>.json.
 */
export interface SiteLocale {
  code: 'it' | 'en' | 'de' | 'fr'
  /** In its own language, for the switcher. */
  name: string
  /** URL prefix: '' for Italian, '/en', '/de', '/fr'. */
  prefix: string
  /** Open Graph locale. */
  og: string
}

export const SITE_LOCALES: readonly SiteLocale[] = [
  { code: 'it', name: 'Italiano', prefix: '', og: 'it_IT' },
  { code: 'en', name: 'English', prefix: '/en', og: 'en_GB' },
  { code: 'de', name: 'Deutsch', prefix: '/de', og: 'de_DE' },
  { code: 'fr', name: 'Français', prefix: '/fr', og: 'fr_FR' },
]

export const DEFAULT_LOCALE = SITE_LOCALES[0]

/** The site language for an Angular LOCALE_ID ("de", "en-US"…); Italian when unknown. */
export function siteLocale(localeId: string): SiteLocale {
  const code = localeId.slice(0, 2).toLowerCase()
  return SITE_LOCALES.find(l => l.code === code) ?? DEFAULT_LOCALE
}
