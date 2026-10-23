import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
} from '@angular/core'
import { RouterOutlet } from '@angular/router'
import { SeoService } from './core/services/seo.service'
import { RouteLoaderComponent } from './layout/route-loader/route-loader.component'

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouteLoaderComponent],
  template: '<app-route-loader /><router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  constructor() {
    inject(SeoService).start(inject(DestroyRef))
  }
}
