import { isPlatformServer } from '@angular/common'
import { HttpInterceptorFn } from '@angular/common/http'
import { inject, PLATFORM_ID } from '@angular/core'
import { EMPTY } from 'rxjs'

/**
 * While prerendering there is no API to call: the request completes at once without a response,
 * so the page is saved in its loading state (skeletons) and the browser loads the live data
 * (prices, reviews) itself. Completing matters: a pending request would stop the page from
 * ever being saved.
 */
export const serverApiInterceptor: HttpInterceptorFn = (req, next) =>
  isPlatformServer(inject(PLATFORM_ID)) ? EMPTY : next(req)
