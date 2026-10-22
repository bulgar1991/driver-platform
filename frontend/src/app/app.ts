import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
} from '@angular/core'
import { RouterOutlet } from '@angular/router'
import { SeoService } from './core/services/seo.service'

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  constructor() {
    inject(SeoService).start(inject(DestroyRef))
  }
}
