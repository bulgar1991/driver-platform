import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { AuthService } from '../services/auth.service'
import { authInterceptor } from './auth.interceptor'

describe('authInterceptor', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { accessToken: 'jwt' } },
      ],
    })
  })

  it('sends the staff token only to the staff API', () => {
    const http = TestBed.inject(HttpClient)
    const backend = TestBed.inject(HttpTestingController)

    http.get('/api/admin/bookings').subscribe()
    http.get('/api/driver/token').subscribe()
    http.get('/api/quotes').subscribe()

    expect(
      backend
        .expectOne('/api/admin/bookings')
        .request.headers.get('Authorization')
    ).toBe('Bearer jwt')
    expect(
      backend
        .expectOne('/api/driver/token')
        .request.headers.has('Authorization')
    ).toBe(false)
    expect(
      backend.expectOne('/api/quotes').request.headers.has('Authorization')
    ).toBe(false)
    backend.verify()
  })
})
