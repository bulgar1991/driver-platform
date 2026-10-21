import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core'
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
} from '@angular/router'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { filter } from 'rxjs'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'

/** Pages of the public site (each section also appears on the home page). */
export const SITE_PAGES = [
  { label: 'Services', path: '/services' },
  { label: 'Tariffs', path: '/tariffs' },
  { label: 'For drivers', path: '/drivers' },
  { label: 'Reviews', path: '/reviews' },
  { label: 'Partners', path: '/partners' },
]

@Component({
  selector: 'app-site-header',
  imports: [
    RouterLink,
    RouterLinkActive,
    ButtonComponent,
    IconComponent,
    LogoComponent,
  ],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.site-header--scrolled]': 'scrolled()' },
})
export class SiteHeaderComponent {
  protected readonly pages = SITE_PAGES
  protected readonly scrolled = signal(false)
  protected readonly open = signal(false)

  constructor() {
    const destroyRef = inject(DestroyRef)
    inject(Router)
      .events.pipe(
        filter(e => e instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => this.open.set(false))

    afterNextRender(() => {
      // One read per frame at most: scroll handlers must not block scrolling.
      let ticking = false
      const onScroll = () => {
        if (!ticking) {
          ticking = true
          requestAnimationFrame(() => {
            this.scrolled.set(window.scrollY > 24)
            ticking = false
          })
        }
      }
      onScroll()
      window.addEventListener('scroll', onScroll, { passive: true })
      destroyRef.onDestroy(() => window.removeEventListener('scroll', onScroll))
    })
  }
}
