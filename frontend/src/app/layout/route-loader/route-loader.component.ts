import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
import { LoadingScreenService } from '@/core/services/loading-screen.service'
import { CarLoaderComponent } from '@/shared/components/car-loader/car-loader.component'

/** Shown only when the next page takes longer than this, so fast navigation does not flicker. */
const SHOW_AFTER_MS = 150

/**
 * While the next page loads (its code is fetched the first time), or the page in another language
 * (LoadingScreenService): the whole screen dims and the gold car drives in the middle, as when the
 * site starts (index.html).
 */
@Component({
  selector: 'app-route-loader',
  imports: [CarLoaderComponent],
  template: `
    @if (visible()) {
      <div class="route-loader">
        <app-car-loader size="lg" />
      </div>
    }
  `,
  styleUrl: './route-loader.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RouteLoaderComponent {
  private readonly navigating = signal(false)
  private readonly screen = inject(LoadingScreenService)
  protected readonly visible = computed(
    () => this.navigating() || this.screen.visible()
  )

  constructor() {
    let timer: ReturnType<typeof setTimeout> | undefined
    const subscription = inject(Router).events.subscribe(event => {
      if (event instanceof NavigationStart) {
        clearTimeout(timer)
        timer = setTimeout(() => this.navigating.set(true), SHOW_AFTER_MS)
      } else if (
        event instanceof NavigationEnd ||
        event instanceof NavigationCancel ||
        event instanceof NavigationError
      ) {
        clearTimeout(timer)
        this.navigating.set(false)
      }
    })
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(timer)
      subscription.unsubscribe()
    })
  }
}
