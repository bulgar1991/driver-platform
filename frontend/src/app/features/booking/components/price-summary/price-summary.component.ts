import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core'
import { LegPrice, Quote, QuoteLeg, VehicleOption } from '@/core/models'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { DurationPipe } from '@/shared/pipes/duration.pipe'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { VEHICLE_INFO } from '../../vehicle-info'
import { TranslatedPipe } from '@/shared/pipes/translated.pipe'

interface LegView {
  title: string
  leg: QuoteLeg
  price: LegPrice | null
}

/** Route and price breakdown, shown next to the map. */
@Component({
  selector: 'app-price-summary',
  imports: [DatePipe, DurationPipe, IconComponent, MoneyPipe, TranslatedPipe],
  templateUrl: './price-summary.component.html',
  styleUrl: './price-summary.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriceSummaryComponent {
  readonly quote = input.required<Quote>()
  readonly vehicle = input<VehicleOption | null>(null)

  protected readonly title = computed(() => {
    const vehicle = this.vehicle()
    return vehicle ? VEHICLE_INFO[vehicle.category].title : null
  })

  protected readonly legs = computed<LegView[]>(() => {
    const quote = this.quote()
    const vehicle = this.vehicle()
    const legs: LegView[] = [
      {
        title: quote.return ? $localize`Outbound` : $localize`Your ride`,
        leg: quote.outbound,
        price: vehicle?.outbound ?? null,
      },
    ]
    if (quote.return) {
      legs.push({
        title: $localize`Return`,
        leg: quote.return,
        price: vehicle?.return ?? null,
      })
    }
    return legs
  })
}
