import { DOCUMENT, isPlatformBrowser } from '@angular/common'
import { inject, Injectable, PLATFORM_ID } from '@angular/core'
import { NavigationEnd, Router } from '@angular/router'
import { filter, firstValueFrom } from 'rxjs'

const KEY = 'md.language-switch'
/** Only a switch made just now counts, not one from an earlier visit in the same tab. */
const MAX_AGE_MS = 30_000
/** How long the scroll keeps up with the page while it settles (prices, map) after a switch. */
const SCROLL_WAIT_MS = 1_500

/** Where the visitor was: in which section, how far into it (texts differ in length by language). */
interface ScrollSpot {
  section: number
  /** 0 = the top of the section at the top of the screen, 1 = its end. */
  into: number
}

interface Carry {
  at: number
  scroll: ScrollSpot | null
  data: Record<string, unknown>
}

/**
 * Each language is its own build, so switching language loads another page. This carries what the
 * visitor had over to it, so the switch feels like a change of language and not of page: the
 * place on the page and whatever pages register (the search in the booking bar).
 *
 * The other language may have been prerendered in the background (speculation rules in
 * index.html) before the click, so the new page reads the carry only once it is shown.
 */
@Injectable({ providedIn: 'root' })
export class LanguageCarryService {
  private readonly document = inject(DOCUMENT)
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID))
  private readonly router = inject(Router)
  private readonly providers = new Map<string, () => unknown>()
  private readonly carried: Promise<Carry | null> = this.browser
    ? this.whenShown().then(() => this.read())
    : Promise.resolve(null)

  constructor() {
    void this.carried.then(
      carry => carry?.scroll && void this.restoreScroll(carry.scroll)
    )
  }

  /** What to carry under `key` when the language changes; returns a function that stops it. */
  provide(key: string, value: () => unknown): () => void {
    this.providers.set(key, value)
    return () => this.providers.delete(key)
  }

  /** What the page in the previous language carried under `key`, if the visitor just switched. */
  async take<T>(key: string): Promise<T | null> {
    const carry = await this.carried
    return (carry?.data[key] as T | undefined) ?? null
  }

  /** Called on the click on another language, just before the browser leaves the page. */
  save(): void {
    const data: Record<string, unknown> = {}
    for (const [key, value] of this.providers) {
      const v = value()
      if (v != null) {
        data[key] = v
      }
    }
    const carry: Carry = { at: Date.now(), scroll: this.scrollSpot(), data }
    try {
      sessionStorage.setItem(KEY, JSON.stringify(carry))
    } catch {
      // Storage blocked (private mode): the switch still works, from the top of the page.
    }
  }

  private read(): Carry | null {
    try {
      const raw = sessionStorage.getItem(KEY)
      sessionStorage.removeItem(KEY)
      const carry = raw ? (JSON.parse(raw) as Carry) : null
      if (!carry || Date.now() - carry.at >= MAX_AGE_MS) {
        return null
      }
      // Styles: no fade-up of what the visitor was already looking at (_motion.scss).
      this.document.documentElement.setAttribute('data-language-carried', '')
      return carry
    } catch {
      return null
    }
  }

  private whenShown(): Promise<void> {
    const doc = this.document as Document & { prerendering?: boolean }
    return doc.prerendering
      ? new Promise(resolve =>
          doc.addEventListener('prerenderingchange', () => resolve(), {
            once: true,
          })
        )
      : Promise.resolve()
  }

  /** The page's sections, in order: the same in every language. */
  private sections(): HTMLElement[] {
    return Array.from(
      this.document.querySelectorAll<HTMLElement>('main section')
    )
  }

  private scrollSpot(): ScrollSpot | null {
    if ((this.document.defaultView?.scrollY ?? 0) <= 0) {
      return null
    }
    const sections = this.sections()
    const index = sections.findIndex(s => s.getBoundingClientRect().bottom > 0)
    if (index < 0) {
      return null
    }
    const { top, height } = sections[index].getBoundingClientRect()
    return { section: index, into: height > 0 ? -top / height : 0 }
  }

  /** The scroll position of a spot on this page, or null while its section isn't there yet. */
  private scrollTop(spot: ScrollSpot): number | null {
    const section = this.sections()[spot.section]
    const win = this.document.defaultView
    if (!section || !win) {
      return null
    }
    const { top, height } = section.getBoundingClientRect()
    return Math.max(0, Math.round(win.scrollY + top + spot.into * height))
  }

  /** Back to where the visitor was, keeping up while the page settles. */
  private async restoreScroll(spot: ScrollSpot): Promise<void> {
    const win = this.document.defaultView
    if (!win) {
      return
    }
    // After the router's own scroll to the top of the first page.
    if (!this.router.navigated) {
      await firstValueFrom(
        this.router.events.pipe(filter(e => e instanceof NavigationEnd))
      )
    }
    // The visitor scrolling (or typing) takes over.
    let stopped = false
    const stop = () => (stopped = true)
    const events = ['wheel', 'touchstart', 'keydown'] as const
    events.forEach(e =>
      win.addEventListener(e, stop, { once: true, passive: true })
    )
    const until = Date.now() + SCROLL_WAIT_MS
    // On every frame for a moment: the map and the prices still change the page's height.
    const step = () => {
      if (stopped) {
        return
      }
      const y = this.scrollTop(spot)
      if (y !== null && Math.abs(win.scrollY - y) > 1) {
        win.scrollTo({ top: y, behavior: 'instant' })
      }
      if (Date.now() < until) {
        win.requestAnimationFrame(step)
      } else {
        events.forEach(e => win.removeEventListener(e, stop))
      }
    }
    win.setTimeout(step, 50)
  }
}
