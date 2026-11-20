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
import { ExtraType } from '@/core/models'
import { BookingApiService } from '@/core/services/booking-api.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { VEHICLE_INFO } from '@/features/booking/vehicle-info'
import { VEHICLE_TEXT } from '../../home.content'
import { SiteContentService } from '@/core/services/site-content.service'
import { HEADINGS, VEHICLE_KEY } from '../../editable-content'

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

  private readonly site = inject(SiteContentService)
  protected readonly heading = computed(() => ({
    eyebrow: this.site.text('tariffs.eyebrow', HEADINGS.tariffsEyebrow),
    title: this.site.text('tariffs.title', HEADINGS.tariffsTitle),
  }))
  /** Texts and pictures as changed in Admin → Website content, the built-in ones otherwise. */
  protected readonly cards = computed(() =>
    (this.options()?.vehicles ?? []).map(v => {
      const key = VEHICLE_KEY[v.category] ?? 'vehicle.standard'
      const text = VEHICLE_TEXT[v.category]
      const info = VEHICLE_INFO[v.category] ?? VEHICLE_INFO.Sedan
      return {
        ...v,
        text: text && {
          title: this.site.text(`${key}.title`, text.title),
          models: this.site.text(`${key}.models`, text.models),
          bestFor: this.site.text(`${key}.best-for`, text.bestFor),
        },
        image: this.site.image(`${key}.image`, info.image),
        featured: v.category === 'Estate',
      }
    })
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
