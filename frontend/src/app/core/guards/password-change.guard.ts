import { inject } from '@angular/core'
import { CanActivateChildFn, Router } from '@angular/router'
import { AuthService } from '../services/auth.service'

export const ACCOUNT_PATH = '/admin/account'

/** With a temporary password, every panel page leads to the password page (the API refuses the rest). */
export const passwordChangeGuard: CanActivateChildFn = (_route, state) => {
  const user = inject(AuthService).currentUser()
  if (!user?.mustChangePassword || state.url.startsWith(ACCOUNT_PATH)) {
    return true
  }
  return inject(Router).createUrlTree([ACCOUNT_PATH])
}
