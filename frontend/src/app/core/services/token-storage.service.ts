import { Injectable } from '@angular/core'

const TOKEN_KEY = 'md.access_token'
const EXPIRES_KEY = 'md.access_token_expires'

/** Keeps the staff access token between page reloads. */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  save(token: string, expiresAt: string): void {
    try {
      localStorage.setItem(TOKEN_KEY, token)
      localStorage.setItem(EXPIRES_KEY, expiresAt)
    } catch {
      // Storage can be unavailable (private mode); the session then lasts until reload.
    }
  }

  /** The token, or null when missing or expired. */
  get(): string | null {
    try {
      const token = localStorage.getItem(TOKEN_KEY)
      const expiresAt = localStorage.getItem(EXPIRES_KEY)
      if (!token || !expiresAt || Date.parse(expiresAt) <= Date.now()) {
        return null
      }
      return token
    } catch {
      return null
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(EXPIRES_KEY)
    } catch {
      // Nothing to clear.
    }
  }
}
