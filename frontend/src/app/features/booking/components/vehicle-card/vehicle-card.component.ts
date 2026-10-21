import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core'
import { VehicleOption } from '@/core/models'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { VEHICLE_INFO } from '../../vehicle-info'

@Component({
  selector: 'app-vehicle-card',
  imports: [IconComponent, MoneyPipe],
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
