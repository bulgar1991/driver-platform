import { provideHttpClient, withInterceptors } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { provideRouter } from '@angular/router'
import { HttpClient } from '@angular/common/http'
import { authInterceptor } from '../interceptors/auth.interceptor'
import { CurrentUser, LoginResponse } from '../models'
import { AuthService } from './auth.service'
import { TokenStorageService } from './token-storage.service'

const user: CurrentUser = {
  id: '1',
  email: 'admin@test.local',
  fullName: 'Admin',
  role: 'Admin',
}

describe('AuthService', () => {
  let service: AuthService
  let http: HttpTestingController

  beforeEach(() => {
    localStorage.clear()
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    })
    service = TestBed.inject(AuthService)
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => http.verify())

  it('stores the session after login and sends the token afterwards', () => {
    const expiresAt = new Date(Date.now() + 60_000).toISOString()
    service.login({ email: user.email, password: 'x' }).subscribe()
    http
      .expectOne('/api/auth/login')
      .flush({ accessToken: 'jwt', expiresAt, user } satisfies LoginResponse)

    expect(service.isAuthenticated()).toBe(true)
    expect(service.hasRole('Admin')).toBe(true)
    expect(service.hasRole('Dispatcher')).toBe(false)
    expect(TestBed.inject(TokenStorageService).get()).toBe('jwt')

    TestBed.inject(HttpClient).get('/api/admin/bookings').subscribe()
    const req = http.expectOne('/api/admin/bookings')
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt')
    req.flush([])
  })

  it('restores nothing without a stored token', () => {
    service.restoreSession().subscribe()
    http.expectNone('/api/auth/me')
    expect(service.isAuthenticated()).toBe(false)
  })

  it('drops a stored token the API rejects', () => {
    const storage = TestBed.inject(TokenStorageService)
    storage.save('old', new Date(Date.now() + 60_000).toISOString())

    service.restoreSession().subscribe()
    http
      .expectOne('/api/auth/me')
      .flush(null, { status: 401, statusText: 'Unauthorized' })

    expect(service.isAuthenticated()).toBe(false)
    expect(storage.get()).toBeNull()
  })

  it('ignores an expired stored token', () => {
    TestBed.inject(TokenStorageService).save(
      'old',
      new Date(Date.now() - 1000).toISOString()
    )
    service.restoreSession().subscribe()
    http.expectNone('/api/auth/me')
  })
})
