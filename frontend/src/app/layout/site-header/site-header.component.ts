import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core'
import { NavigationEnd, Router, RouterLink } from '@angular/router'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { filter } from 'rxjs'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'

export const SITE_SECTIONS = [
  { label: 'Services', fragment: 'services' },
  { label: 'Tariffs', fragment: 'tariffs' },
  { label: 'For drivers', fragment: 'drivers' },
  { label: 'Reviews', fragment: 'reviews' },
  { label: 'Partners', fragment: 'partners' },
]

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, ButtonComponent, IconComponent, LogoComponent],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.site-header--scrolled]': 'scrolled()' },
})
export class SiteHeaderComponent {
  protected readonly sections = SITE_SECTIONS
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
