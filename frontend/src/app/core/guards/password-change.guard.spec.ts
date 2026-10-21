import { signal } from '@angular/core'
import { TestBed } from '@angular/core/testing'
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router'
import { CurrentUser } from '../models'
import { AuthService } from '../services/auth.service'
import { passwordChangeGuard } from './password-change.guard'

describe('passwordChangeGuard', () => {
  const user = signal<CurrentUser | null>(null)

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser: user } },
      ],
    })
  })

  const run = (url: string) =>
    TestBed.runInInjectionContext(() =>
      passwordChangeGuard(
        {} as ActivatedRouteSnapshot,
        { url } as RouterStateSnapshot
      )
    )

  it('sends users with a temporary password to the account page', () => {
    user.set({
      id: '1',
      email: 'a@b.c',
      fullName: 'A',
      role: 'Dispatcher',
      mustChangePassword: true,
    })

    const result = run('/admin/bookings') as UrlTree

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/admin/account')
    expect(run('/admin/account')).toBe(true)
  })

  it('lets everyone else through', () => {
    user.set({ id: '1', email: 'a@b.c', fullName: 'A', role: 'Dispatcher' })

    expect(run('/admin/bookings')).toBe(true)
  })
})
