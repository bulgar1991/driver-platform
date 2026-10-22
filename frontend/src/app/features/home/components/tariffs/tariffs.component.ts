import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { RouterLink } from '@angular/router'
import { catchError, of } from 'rxjs'
import { ExtraType, VehicleCategory } from '@/core/models'
import { BookingApiService } from '@/core/services/booking-api.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { VEHICLE_TEXT } from '../../home.content'

/** Illustrations in src/assets/images/cars (replace with photos of the real cars when there are some). */
const CAR_IMAGE: Partial<Record<VehicleCategory, string>> = {
  Sedan: '/assets/images/cars/sedan.svg',
  Estate: '/assets/images/cars/suv.svg',
  Van: '/assets/images/cars/van.svg',
}

const EXTRA_LABEL: Record<ExtraType, string> = {
  ChildSeat: $localize`Child seat`,
  NameSign: $localize`Name sign in arrivals`,
  ExtraLuggage: $localize`Extra large bag`,
}

/** Live tariffs from the API: what the price calculation actually uses. */
@Component({
  selector: 'app-tariffs',
  imports: [
    RouterLink,
    ButtonComponent,
    IconComponent,
    ImageComponent,
    SectionHeadingComponent,
    RevealDirective,
    MoneyPipe,
  ],
  templateUrl: './tariffs.component.html',
  styleUrl: './tariffs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TariffsComponent {
  /** The section is the whole page. */
  readonly page = input(false)
  private readonly options = toSignal(
    inject(BookingApiService)
      .options()
      .pipe(catchError(() => of(null))),
    { initialValue: undefined }
  )

  protected readonly loading = computed(() => this.options() === undefined)
  protected readonly failed = computed(() => this.options() === null)

  protected readonly cards = computed(() =>
    (this.options()?.vehicles ?? []).map(v => ({
      ...v,
      text: VEHICLE_TEXT[v.category],
      image: CAR_IMAGE[v.category] ?? CAR_IMAGE.Sedan!,
      featured: v.category === 'Estate',
    }))
  )

  protected readonly extras = computed(() =>
    Object.entries(this.options()?.extras ?? {}).map(([type, amount]) => ({
      label: EXTRA_LABEL[type as ExtraType] ?? type,
      amount: amount ?? 0,
    }))
  )

  protected readonly surcharges = computed(
    () => this.options()?.surcharges ?? []
  )
  protected readonly returnDiscount = computed(
    () => this.options()?.returnDiscountPercent ?? 0
  )
}
