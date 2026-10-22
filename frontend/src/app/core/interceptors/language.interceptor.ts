import { HttpInterceptorFn } from '@angular/common/http'
import { inject, LOCALE_ID } from '@angular/core'
import { siteLocale } from '../i18n/locales'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/**
 * Tells the API the site language, so price lines, surcharge names and error messages come back
 * in it. The staff API stays in English.
 */
export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  const api = inject(API_BASE_URL)
  if (!req.url.startsWith(api) || req.url.startsWith(api + '/admin')) {
    return next(req)
  }
  const language = siteLocale(inject(LOCALE_ID)).code
  return next(req.clone({ setHeaders: { 'Accept-Language': language } }))
}
