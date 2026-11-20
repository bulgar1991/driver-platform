import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core'
import { VehicleOption } from '@/core/models'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { SiteContentService } from '@/core/services/site-content.service'
import { VEHICLE_INFO, VEHICLE_KEY } from '../../vehicle-info'
import { SITE_CURRENCY } from '@/core/currency'

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
  readonly currency = input(SITE_CURRENCY)
  readonly chosen = output()

  private readonly site = inject(SiteContentService)

  /** Name, description and picture as changed in Admin → Website content, the built-in ones otherwise. */
  protected readonly info = computed(() => {
    const category = this.vehicle().category
    const info = VEHICLE_INFO[category]
    const key = VEHICLE_KEY[category]
    return {
      title: this.site.text(`${key}.title`, info.title),
      description: this.site.text(`${key}.description`, info.description),
      image: this.site.image(`${key}.image`, info.image),
    }
  })
}
