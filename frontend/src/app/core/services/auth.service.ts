import { HttpClient } from '@angular/common/http'
import { computed, inject, Injectable, signal } from '@angular/core'
import { Router } from '@angular/router'
import { catchError, map, Observable, of, tap } from 'rxjs'
import {
  ChangePasswordRequest,
  CurrentUser,
  LoginRequest,
  LoginResponse,
  StaffRole,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'
import { TokenStorageService } from './token-storage.service'

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient)
  private readonly router = inject(Router)
  private readonly storage = inject(TokenStorageService)
  private readonly apiUrl = inject(API_BASE_URL)

  private readonly user = signal<CurrentUser | null>(null)
  private token: string | null = null

  readonly currentUser = this.user.asReadonly()
  readonly isAuthenticated = computed(() => this.user() !== null)

  get accessToken(): string | null {
    return this.token
  }

  hasRole(...roles: StaffRole[]): boolean {
    const user = this.user()
    return user !== null && roles.includes(user.role)
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/auth/login`, request)
      .pipe(
        tap(response => {
          this.token = response.accessToken
          this.storage.save(response.accessToken, response.expiresAt)
          this.user.set(response.user)
        }),
        map(response => response.user)
      )
  }

  /** Returns the user with a fresh token; the server ends every other session. */
  changePassword(request: ChangePasswordRequest): Observable<CurrentUser> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/auth/change-password`, request)
      .pipe(
        tap(response => {
          this.token = response.accessToken
          this.storage.save(response.accessToken, response.expiresAt)
          this.user.set(response.user)
        }),
        map(response => response.user)
      )
  }

  /** Loads the signed-in user from a stored token; runs once at startup. */
  restoreSession(): Observable<void> {
    const token = this.storage.get()
    if (!token) {
      return of(undefined)
    }
    this.token = token
    return this.http.get<CurrentUser>(`${this.apiUrl}/auth/me`).pipe(
      tap(user => this.user.set(user)),
      map(() => undefined),
      catchError(() => {
        this.clearSession()
        return of(undefined)
      })
    )
  }

  logout(): void {
    this.clearSession()
    void this.router.navigateByUrl('/login')
  }

  private clearSession(): void {
    this.token = null
    this.storage.clear()
    this.user.set(null)
  }
}
