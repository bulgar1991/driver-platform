import { inject } from '@angular/core'
import { CanMatchFn, Router } from '@angular/router'
import { stripLocalePrefix } from '../i18n/locales'

/**
 * Another language's URL reached this build: only on `ng serve`, which serves Italian alone
 * (in production the web server sends /de/… to the German build). Shows the page in this
 * language instead of "Not found".
 */
export const localePrefixGuard: CanMatchFn = (_route, segments) => {
  const url = '/' + segments.map(s => s.path).join('/')
  const stripped = stripLocalePrefix(url)
  return stripped === url ? false : inject(Router).parseUrl(stripped)
}
