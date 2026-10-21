import { signal } from '@angular/core'
import { TestBed } from '@angular/core/testing'
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router'
import { CurrentUser, StaffRole } from '../models'
import { AuthService } from '../services/auth.service'
import { authGuard } from './auth.guard'
import { guestGuard } from './guest.guard'
import { roleGuard } from './role.guard'

function setup(role: StaffRole | null) {
  const user = signal<CurrentUser | null>(
    role ? { id: '1', email: 'a@b.c', fullName: 'A', role } : null
  )
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      {
        provide: AuthService,
        useValue: {
          isAuthenticated: () => user() !== null,
          hasRole: (...roles: StaffRole[]) =>
            user() !== null && roles.includes(user()!.role),
        },
      },
    ],
  })
}

const route = {} as ActivatedRouteSnapshot
const state = (url: string) => ({ url }) as RouterStateSnapshot
const serialize = (result: unknown) =>
  TestBed.inject(Router).serializeUrl(result as UrlTree)

describe('authGuard', () => {
  it('sends guests to login with a return url', () => {
    setup(null)
    const result = TestBed.runInInjectionContext(() =>
      authGuard(route, state('/admin/drivers'))
    )
    expect(serialize(result)).toBe('/login?returnUrl=%2Fadmin%2Fdrivers')
  })

  it('lets signed-in staff through', () => {
    setup('Dispatcher')
    const result = TestBed.runInInjectionContext(() =>
      authGuard(route, state('/admin'))
    )
    expect(result).toBe(true)
  })
})

describe('roleGuard', () => {
  it('allows a matching role', () => {
    setup('Admin')
    const result = TestBed.runInInjectionContext(() =>
      roleGuard('Admin')(route, state('/admin/pricing'))
    )
    expect(result).toBe(true)
  })

  it('redirects other roles to the dashboard', () => {
    setup('Dispatcher')
    const result = TestBed.runInInjectionContext(() =>
      roleGuard('Admin')(route, state('/admin/pricing'))
    )
    expect(serialize(result)).toBe('/admin')
  })
})

describe('guestGuard', () => {
  it('sends signed-in staff to the dashboard', () => {
    setup('Admin')
    const result = TestBed.runInInjectionContext(() =>
      guestGuard(route, state('/login'))
    )
    expect(serialize(result)).toBe('/admin')
  })
})
