import { inject } from '@angular/core'
import { CanActivateChildFn, Router } from '@angular/router'
import { AuthService } from '../services/auth.service'

export const ACCOUNT_PATH = '/admin/account'

/**
 * With a temporary password, or before a required two-step sign-in is set up, every panel page
 * leads to My account (the API refuses the rest).
 */
export const passwordChangeGuard: CanActivateChildFn = (_route, state) => {
  const user = inject(AuthService).currentUser()
  const blocked = user?.mustChangePassword || user?.twoFactorSetupRequired
  if (!blocked || state.url.startsWith(ACCOUNT_PATH)) {
    return true
  }
  return inject(Router).createUrlTree([ACCOUNT_PATH])
}
