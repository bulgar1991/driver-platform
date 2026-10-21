import { inject } from '@angular/core'
import { CanActivateFn, Router } from '@angular/router'
import { AuthService } from '../services/auth.service'

/** Keeps signed-in staff away from the login page. */
export const guestGuard: CanActivateFn = () =>
  !inject(AuthService).isAuthenticated() ||
  inject(Router).createUrlTree(['/admin'])
