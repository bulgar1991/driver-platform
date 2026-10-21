import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http'
import { inject } from '@angular/core'
import { catchError, throwError } from 'rxjs'
import { AuthService } from '../services/auth.service'

/** Signs the user out when the API rejects their token. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService)
  return next(req).pipe(
    catchError((error: unknown) => {
      const isLogin = req.url.endsWith('/auth/login')
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !isLogin &&
        auth.isAuthenticated()
      ) {
        auth.logout()
      }
      return throwError(() => error)
    })
  )
}
