import { inject } from '@angular/core'
import { CanActivateFn, Router } from '@angular/router'
import { StaffRole } from '../models'
import { AuthService } from '../services/auth.service'

/** Allows only the given roles; use after authGuard. Others land on the dashboard. */
export const roleGuard =
  (...roles: StaffRole[]): CanActivateFn =>
  () =>
    inject(AuthService).hasRole(...roles) ||
    inject(Router).createUrlTree(['/admin'])
