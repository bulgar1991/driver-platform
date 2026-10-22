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

/**
 * IT · EN · DE · FR. Each language is its own build, so the links are plain links (a full page
 * load) to the same page under the other prefix.
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
}
