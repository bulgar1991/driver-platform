import { HttpInterceptorFn } from '@angular/common/http'
import { inject } from '@angular/core'
import { AuthService } from '../services/auth.service'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Only the staff API gets the token; public pages (booking, driver link, review) never carry it. */
const STAFF_PATHS = ['/admin', '/auth']

/** Adds the staff access token to staff API requests. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).accessToken
  const api = inject(API_BASE_URL)
  const staff = STAFF_PATHS.some(path => req.url.startsWith(api + path))
  if (!token || !staff) {
    return next(req)
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
}
