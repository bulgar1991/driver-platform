import { DOCUMENT } from '@angular/common'
import { inject, Injectable, signal } from '@angular/core'

/**
 * The full-screen car (RouteLoaderComponent) on demand, for waits the router doesn't see: the
 * browser leaving for the page in another language.
 */
@Injectable({ providedIn: 'root' })
export class LoadingScreenService {
  private readonly shown = signal(false)
  readonly visible = this.shown.asReadonly()

  constructor() {
    // Back to this page from the browser's memory (back button): it was left with the car showing.
    inject(DOCUMENT).defaultView?.addEventListener('pageshow', e => {
      if (e.persisted) {
        this.shown.set(false)
      }
    })
  }

  show(): void {
    this.shown.set(true)
  }
}
