import { DOCUMENT } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  LOCALE_ID,
} from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { NavigationEnd, Router } from '@angular/router'
import { filter, map } from 'rxjs'
import {
  SITE_LOCALES,
  siteLocale,
  stripLocalePrefix,
} from '@/core/i18n/locales'
import { LanguageCarryService } from '@/core/services/language-carry.service'
import { LoadingScreenService } from '@/core/services/loading-screen.service'

/**
 * IT · EN · DE · FR. Each language is its own build, so the links are plain links (a full page
 * load) to the same page under the other prefix. To make that feel instant: the other language is
 * loaded in the background on hover (speculation rules in index.html, or a prefetch where the
 * browser has none), the pages cross-fade (@view-transition in styles.scss), and the scroll
 * position and the search carry over (LanguageCarryService). Until the new page is there, the
 * full-screen car shows, and the cross-fade goes from it to the new page.
 */
@Component({
  selector: 'app-language-switch',
  templateUrl: './language-switch.component.html',
  styleUrl: './language-switch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.language-switch--menu]': "variant() === 'menu'" },
})
export class LanguageSwitchComponent {
  /** Compact codes in the header bar, full names in the mobile menu. */
  readonly variant = input<'bar' | 'menu'>('bar')

  private readonly router = inject(Router)
  private readonly document = inject(DOCUMENT)
  private readonly carry = inject(LanguageCarryService)
  private readonly loadingScreen = inject(LoadingScreenService)
  private readonly prefetched = new Set<string>()
  protected readonly current = siteLocale(inject(LOCALE_ID))
  private readonly url = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this.router.url)
    ),
    { initialValue: this.router.url }
  )

  protected readonly links = computed(() => {
    // The query (if any) is kept; a stray language prefix is dropped, so they never pile up.
    const url = stripLocalePrefix(this.url())
    const path = url === '/' ? '' : url
    return SITE_LOCALES.map(l => ({ ...l, href: `${l.prefix}${path}` || '/' }))
  })

  protected leave(event: MouseEvent, current: boolean): void {
    // Ctrl / ⌘ / Shift / middle click opens another tab: this page stays as it is.
    if (
      current ||
      event.button !== 0 ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey ||
      event.altKey
    ) {
      return
    }
    this.carry.save()
    this.loadingScreen.show()
  }

  /** Browsers without speculation rules (Safari, Firefox): at least the page is fetched early. */
  protected prefetch(href: string): void {
    const supports = (
      HTMLScriptElement as { supports?: (type: string) => boolean }
    ).supports
    if (supports?.('speculationrules') || this.prefetched.has(href)) {
      return
    }
    this.prefetched.add(href)
    const link = this.document.createElement('link')
    link.rel = 'prefetch'
    link.href = href
    this.document.head.appendChild(link)
  }
}
