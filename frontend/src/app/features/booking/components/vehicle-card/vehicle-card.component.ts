import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core'
import { VehicleOption } from '@/core/models'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { VEHICLE_INFO } from '../../vehicle-info'

/** One car in the results: the drawing, what fits, the price and Select. */
@Component({
  selector: 'app-vehicle-card',
  imports: [IconComponent, ImageComponent, MoneyPipe],
  templateUrl: './vehicle-card.component.html',
  styleUrl: './vehicle-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehicleCardComponent {
  readonly vehicle = input.required<VehicleOption>()
  readonly selected = input(false)
  readonly currency = input('EUR')
  readonly chosen = output()

  protected readonly info = computed(
    () => VEHICLE_INFO[this.vehicle().category]
  )
}
