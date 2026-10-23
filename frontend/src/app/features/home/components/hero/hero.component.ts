import { ChangeDetectionStrategy, Component, inject } from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { FormBuilder } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { catchError, of } from 'rxjs'
import { IMAGES } from '@/core/images.generated'
import { BookingApiService } from '@/core/services/booking-api.service'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import {
  createTripForm,
  DEFAULT_MIN_LEAD_HOURS,
  tripToParams,
} from '@/features/booking/booking-form'
import { SearchBarComponent } from '@/features/booking/components/search-bar/search-bar.component'
import { TRUST_POINTS } from '../../home.content'

/** The banner: background, title and the search bar, which opens the booking page filled in. */
@Component({
  selector: 'app-hero',
  imports: [RouterLink, IconComponent, ImageComponent, SearchBarComponent],
  templateUrl: './hero.component.html',
  styleUrl: './hero.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroComponent {
  protected readonly trust = TRUST_POINTS
  protected readonly images = IMAGES
  private readonly router = inject(Router)

  private readonly options = toSignal(
    inject(BookingApiService)
      .options()
      .pipe(catchError(() => of(null)))
  )
  protected readonly returnDiscount = () =>
    this.options()?.returnDiscountPercent ?? null
  protected readonly trip = createTripForm(
    inject(FormBuilder),
    () => this.options()?.minLeadTimeHours ?? DEFAULT_MIN_LEAD_HOURS
  )

  protected search(): void {
    void this.router.navigate(['/book'], {
      queryParams: tripToParams(this.trip),
    })
  }
}
