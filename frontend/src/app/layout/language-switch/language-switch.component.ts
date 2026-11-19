import {
  CdkConnectedOverlay,
  CdkOverlayOrigin,
  ConnectedPosition,
} from '@angular/cdk/overlay'
import { DOCUMENT } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  LOCALE_ID,
  signal,
  viewChild,
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
import { IconComponent } from '@/shared/components/icon/icon.component'

/**
 * English, French, German, Italian: a dropdown in the header, footer, sign-in and panel, the full
 * names in a row in the mobile menu. Each language is its own build, so the links are plain links (a full page
 * load) to the same page under the other prefix. To make that feel instant: the other language is
 * loaded in the background on hover (speculation rules in index.html, or a prefetch where the
 * browser has none), the pages cross-fade (@view-transition in styles.scss), and the scroll
 * position and the search carry over (LanguageCarryService). Until the new page is there, the
 * full-screen car shows, and the cross-fade goes from it to the new page.
 */
@Component({
  selector: 'app-language-switch',
  imports: [CdkConnectedOverlay, CdkOverlayOrigin, IconComponent],
  templateUrl: './language-switch.component.html',
  styleUrl: './language-switch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitchComponent {
  /** A dropdown in bars (header, footer, sign-in, panel); the full names in the mobile menu. */
  readonly variant = input<'bar' | 'menu'>('bar')

  protected readonly open = signal(false)
  private readonly toggle = viewChild<ElementRef<HTMLElement>>('toggle')
  /** Under the button, right-aligned; above it when there is no room below (e.g. in the footer). */
  protected readonly positions: ConnectedPosition[] = [
    {
      originX: 'end',
      originY: 'bottom',
      overlayX: 'end',
      overlayY: 'top',
      offsetY: 8,
    },
    {
      originX: 'end',
      originY: 'top',
      overlayX: 'end',
      overlayY: 'bottom',
      offsetY: -8,
    },
    {
      originX: 'start',
      originY: 'bottom',
      overlayX: 'start',
      overlayY: 'top',
      offsetY: 8,
    },
    {
      originX: 'start',
      originY: 'top',
      overlayX: 'start',
      overlayY: 'bottom',
      offsetY: -8,
    },
  ]

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

  /** A click outside closes the menu, except on its button (which toggles it). */
  protected closeOutside(event: MouseEvent): void {
    if (!this.toggle()?.nativeElement.contains(event.target as Node)) {
      this.open.set(false)
    }
  }

  protected keydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.open.set(false)
      this.toggle()?.nativeElement.focus()
    }
  }

  protected leave(event: MouseEvent, current: boolean): void {
    if (current) {
      this.open.set(false)
    }
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
