import { DOCUMENT, isPlatformBrowser } from '@angular/common'
import { HttpClient } from '@angular/common/http'
import { inject, Injectable, LOCALE_ID, PLATFORM_ID } from '@angular/core'
import { NavigationEnd, Router } from '@angular/router'
import { filter } from 'rxjs'
import { siteLocale } from '../i18n/locales'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Pages with a private link (booking, review, driver) and the panel: never counted. */
const PRIVATE_PATH = /\/(my-booking|review|driver|admin|login|dev)(\/|$)/

/**
 * Counts the pages seen on the public site for Admin → Visitors (our own server, no cookies).
 * The server adds the IP, place, device and browser.
 */
@Injectable({ providedIn: 'root' })
export class VisitTrackerService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)
  private readonly router = inject(Router)
  private readonly document = inject(DOCUMENT)
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID))
  private readonly language = siteLocale(inject(LOCALE_ID)).code
  private started = false

  start(): void {
    if (!this.browser || this.started) {
      return
    }
    this.started = true
    // The site the visitor came from, for the first page only.
    let referrer: string | null = this.document.referrer || null
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => {
        const path = this.document.location.pathname
        if (PRIVATE_PATH.test(path)) {
          return
        }
        this.http
          .post(`${this.apiUrl}/visits`, {
            path,
            referrer,
            language: this.language,
          })
          .subscribe({ error: () => undefined })
        referrer = null
      })
  }
}
