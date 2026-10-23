import { ChangeDetectionStrategy, Component, input } from '@angular/core'

/**
 * A car driving on a road, shown while something loads. The styles are global
 * (styles/_loader.scss) because index.html shows the same car before the app has started.
 */
@Component({
  selector: 'app-car-loader',
  templateUrl: './car-loader.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'car-loader',
    role: 'status',
    '[class.car-loader--sm]': "size() === 'sm'",
    '[class.car-loader--lg]': "size() === 'lg'",
  },
})
export class CarLoaderComponent {
  /** sm: inline next to a short text; md: in a page; lg: the whole screen. */
  readonly size = input<'sm' | 'md' | 'lg'>('md')
  /** Shown under the car (next to it when small); without one, screen readers hear "Loading…". */
  readonly label = input<string | null>(null)
}
