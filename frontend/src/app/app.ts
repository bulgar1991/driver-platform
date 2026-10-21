import { ViewportScroller } from '@angular/common'
import { ChangeDetectionStrategy, Component, inject } from '@angular/core'
import { RouterOutlet } from '@angular/router'

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  constructor() {
    // Anchor links stop below the sticky header (Angular ignores CSS scroll-margin).
    inject(ViewportScroller).setOffset([0, 80])
  }
}
