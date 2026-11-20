import { DOCUMENT, isPlatformBrowser } from '@angular/common'
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core'
import { NavigationEnd, Router } from '@angular/router'
import { filter } from 'rxjs'

/** The Google Tag Manager container of this site (GTM_ID, per server at build time); empty: none. */
export const GTM_CONTAINER =
  typeof GTM_ID === 'string' && /^GTM-[A-Z0-9]+$/.test(GTM_ID.trim())
    ? GTM_ID.trim()
    : ''

export type ConsentChoice = 'granted' | 'denied'

/** Bump when the banner's purposes change: everybody is asked again. */
const CONSENT_KEY = 'cookie-consent-v1'

/**
 * Pages with a private link in their address (a booking, a review, the driver's shifts): never sent
 * to Google, so the tokens in those links stay with their owner.
 */
const PRIVATE_PATH = /\/(my-booking|review|driver|admin|login|dev)(\/|$)/

type Gtag = (...args: unknown[]) => void
type GtmWindow = Window & { dataLayer?: unknown[]; gtag?: Gtag }

/**
 * Google Tag Manager with Google Consent Mode v2. Analytics and ad storage start "denied"; the
 * cookie banner asks the visitor, the choice is kept in this browser and can be changed from the
 * footer. Loaded from code (not an inline script) so the Content-Security-Policy stays strict.
 */
@Injectable({ providedIn: 'root' })
export class ConsentService {
  private readonly document = inject(DOCUMENT)
  private readonly router = inject(Router)
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID))
  private started = false

  /** The visitor's choice; null: not asked yet (the banner shows). */
  readonly choice = signal<ConsentChoice | null>(null)
  /** The banner was opened again from the footer. */
  readonly reopened = signal(false)
  readonly enabled = GTM_CONTAINER !== ''

  /** Once, from the public pages. Nothing happens without a container or on a private page. */
  start(): void {
    if (!this.enabled || !this.browser || this.started) {
      return
    }
    if (PRIVATE_PATH.test(this.document.location.pathname)) {
      return
    }
    this.started = true
    const win = this.document.defaultView as GtmWindow
    const dataLayer = (win.dataLayer = win.dataLayer ?? [])
    // gtag must push the arguments object itself, as Google's snippet does.
    const gtag: Gtag = function () {
      // eslint-disable-next-line prefer-rest-params
      dataLayer.push(arguments)
    }
    win.gtag = gtag

    gtag('consent', 'default', {
      ad_storage: 'denied',
      ad_user_data: 'denied',
      ad_personalization: 'denied',
      analytics_storage: 'denied',
      functionality_storage: 'granted',
      security_storage: 'granted',
      wait_for_update: 500,
    })
    const saved = this.read()
    if (saved) {
      this.choice.set(saved)
      this.update(saved)
    }

    gtag('js', new Date())
    dataLayer.push({ 'gtm.start': Date.now(), event: 'gtm.js' })
    const script = this.document.createElement('script')
    script.async = true
    script.src = `https://www.googletagmanager.com/gtm.js?id=${GTM_CONTAINER}`
    this.document.head.appendChild(script)

    // Page views of the single-page site, public pages only.
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => {
        if (!PRIVATE_PATH.test(e.urlAfterRedirects)) {
          dataLayer.push({
            event: 'page_view',
            page_path: e.urlAfterRedirects.split('?')[0],
            page_title: this.document.title,
          })
        }
      })
  }

  accept(): void {
    this.decide('granted')
  }

  reject(): void {
    this.decide('denied')
  }

  /** "Cookie settings" in the footer. */
  reopen(): void {
    this.reopened.set(true)
  }

  private decide(choice: ConsentChoice): void {
    this.choice.set(choice)
    this.reopened.set(false)
    try {
      localStorage.setItem(
        CONSENT_KEY,
        JSON.stringify({ choice, at: new Date().toISOString() })
      )
    } catch {
      // Private mode: the choice lasts for this visit.
    }
    this.update(choice)
  }

  private update(choice: ConsentChoice): void {
    const gtag = (this.document.defaultView as GtmWindow | null)?.gtag
    gtag?.('consent', 'update', {
      ad_storage: choice,
      ad_user_data: choice,
      ad_personalization: choice,
      analytics_storage: choice,
    })
  }

  private read(): ConsentChoice | null {
    try {
      const value = JSON.parse(localStorage.getItem(CONSENT_KEY) ?? 'null')
      return value?.choice === 'granted' || value?.choice === 'denied'
        ? value.choice
        : null
    } catch {
      return null
    }
  }
}
