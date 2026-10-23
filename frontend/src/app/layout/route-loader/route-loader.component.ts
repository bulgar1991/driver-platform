import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core'
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  Router,
} from '@angular/router'
import { CarLoaderComponent } from '@/shared/components/car-loader/car-loader.component'

/** Shown only when the next page takes longer than this, so fast navigation does not flicker. */
const SHOW_AFTER_MS = 150

/**
 * While the next page loads (its code is fetched the first time), a small car drives along the
 * top edge of the screen.
 */
@Component({
  selector: 'app-route-loader',
  imports: [CarLoaderComponent],
  template: `
    @if (visible()) {
      <div class="route-loader">
        <app-car-loader class="route-loader__car" size="sm" />
      </div>
    }
  `,
  styleUrl: './route-loader.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RouteLoaderComponent {
  protected readonly visible = signal(false)

  constructor() {
    let timer: ReturnType<typeof setTimeout> | undefined
    const subscription = inject(Router).events.subscribe(event => {
      if (event instanceof NavigationStart) {
        clearTimeout(timer)
        timer = setTimeout(() => this.visible.set(true), SHOW_AFTER_MS)
      } else if (
        event instanceof NavigationEnd ||
        event instanceof NavigationCancel ||
        event instanceof NavigationError
      ) {
        clearTimeout(timer)
        this.visible.set(false)
      }
    })
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(timer)
      subscription.unsubscribe()
    })
  }
}
