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
  TwoFactorChallenge,
  TwoFactorEnabled,
  TwoFactorSetup,
  TwoFactorStatus,
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

  /** Signs in, or returns the challenge for the second step when two-step sign-in is on. */
  login(request: LoginRequest): Observable<CurrentUser | TwoFactorChallenge> {
    return this.http
      .post<LoginResponse | TwoFactorChallenge>(
        `${this.apiUrl}/auth/login`,
        request
      )
      .pipe(
        map(response =>
          'twoFactorRequired' in response ? response : this.start(response)
        )
      )
  }

  /** The second step: the authenticator app's code, a code from the email, or a recovery code. */
  verifyTwoFactor(
    challengeToken: string,
    code: string
  ): Observable<CurrentUser> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/auth/login/two-factor`, {
        challengeToken,
        code,
      })
      .pipe(map(response => this.start(response)))
  }

  sendEmailCode(challengeToken: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/auth/login/two-factor/email`, {
      challengeToken,
    })
  }

  /** Returns the user with a fresh token; the server ends every other session. */
  changePassword(request: ChangePasswordRequest): Observable<CurrentUser> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/auth/change-password`, request)
      .pipe(map(response => this.start(response)))
  }

  twoFactorStatus(): Observable<TwoFactorStatus> {
    return this.http.get<TwoFactorStatus>(`${this.apiUrl}/auth/two-factor`)
  }

  setUpTwoFactor(): Observable<TwoFactorSetup> {
    return this.http.post<TwoFactorSetup>(
      `${this.apiUrl}/auth/two-factor/setup`,
      null
    )
  }

  /** Turns it on with the app's first code; the new session opens the whole panel. */
  enableTwoFactor(code: string): Observable<string[]> {
    return this.http
      .post<TwoFactorEnabled>(`${this.apiUrl}/auth/two-factor/enable`, {
        code,
      })
      .pipe(
        tap(r => this.start(r.session)),
        map(r => r.recoveryCodes)
      )
  }

  disableTwoFactor(password: string, code: string): Observable<CurrentUser> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/auth/two-factor/disable`, {
        password,
        code,
      })
      .pipe(map(response => this.start(response)))
  }

  newRecoveryCodes(code: string): Observable<string[]> {
    return this.http
      .post<{
        recoveryCodes: string[]
      }>(`${this.apiUrl}/auth/two-factor/recovery-codes`, { code })
      .pipe(map(r => r.recoveryCodes))
  }

  /** Saves the user's own order of the panel menu (page paths); an empty list is the built-in order. */
  setMenuOrder(order: string[]): Observable<void> {
    return this.http
      .put<void>(`${this.apiUrl}/auth/menu`, { order })
      .pipe(
        tap(() => this.user.update(u => (u ? { ...u, menuOrder: order } : u)))
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

  private start(response: LoginResponse): CurrentUser {
    this.token = response.accessToken
    this.storage.save(response.accessToken, response.expiresAt)
    this.user.set(response.user)
    return response.user
  }

  private clearSession(): void {
    this.token = null
    this.storage.clear()
    this.user.set(null)
  }
}
