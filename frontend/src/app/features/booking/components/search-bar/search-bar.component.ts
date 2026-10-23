import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core'
import { ReactiveFormsModule } from '@angular/forms'
import { todayInItaly } from '@/core/utils/italy-time'
import { CounterComponent } from '@/shared/components/counter/counter.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { AddressInputComponent } from '../address-input/address-input.component'
import { childSeatLimit, TripForm } from '../../booking-form'

/**
 * One bar with the whole search, as on the big travel sites: one way or return, from, to, when,
 * passengers, bags and child seats, then Search. Used on top of the booking, on the home page and on its own page.
 */
@Component({
  selector: 'app-search-bar',
  imports: [
    ReactiveFormsModule,
    AddressInputComponent,
    CounterComponent,
    IconComponent,
    MoneyPipe,
  ],
  templateUrl: './search-bar.component.html',
  styleUrl: './search-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'closeOutside($event)',
    '(document:keydown.escape)': 'peopleOpen.set(false)',
  },
})
export class SearchBarComponent {
  readonly form = input.required<TripForm>()
  /** Shown next to "Return" when the return trip is cheaper. */
  readonly returnDiscount = input<number | null>(null)
  /** Price of one child seat, shown next to them. */
  readonly childSeatPrice = input<number | null>(null)
  /** Every field is filled and valid. */
  readonly searched = output()

  protected readonly minDate = todayInItaly()
  protected readonly peopleOpen = signal(false)
  /** Search was pressed: from now on, every missing field says so. */
  protected readonly submitted = signal(false)
  private readonly people = viewChild<ElementRef<HTMLElement>>('people')

  protected readonly t = {
    pickAddress: $localize`Pick an address from the list`,
    returnMissing: $localize`Enter the return date and time`,
    returnBeforeOutbound: $localize`The return must be later than the outbound trip.`,
    tooSoonOneHour: $localize`Book at least 1 hour ahead.`,
    tooSoon: (hours: number) =>
      $localize`Book at least ${hours}:hours: hours ahead.`,
    childSeats: $localize`At most one child seat per passenger, and three per car.`,
  }

  protected shows(name: 'pickup' | 'dropoff'): boolean {
    const control = this.form().controls[name]
    return control.invalid && (control.touched || this.submitted())
  }

  /** Why the date or time cannot be used, if anything. */
  protected get whenError(): string | null {
    const form = this.form()
    const tooSoon = form.getError('tooSoon') as { hours: number } | null
    if (tooSoon) {
      return tooSoon.hours === 1
        ? this.t.tooSoonOneHour
        : this.t.tooSoon(tooSoon.hours)
    }
    return null
  }

  protected get returnError(): string | null {
    const form = this.form()
    if (form.hasError('returnBeforeOutbound')) {
      return this.t.returnBeforeOutbound
    }
    const touched =
      form.controls.returnDate.touched || form.controls.returnTime.touched
    return form.hasError('returnRequired') && (touched || this.submitted())
      ? this.t.returnMissing
      : null
  }

  /** The first problem to fix, shown under the bar. */
  protected get message(): string | null {
    if (this.shows('pickup') || this.shows('dropoff')) {
      return this.t.pickAddress
    }
    if (this.form().hasError('childSeats')) {
      return this.t.childSeats
    }
    return this.whenError ?? this.returnError
  }

  protected get passengers(): number {
    return this.form().controls.passengers.value
  }

  protected get bags(): number {
    const { largeLuggage, smallLuggage } = this.form().getRawValue()
    return largeLuggage + smallLuggage
  }

  protected get seats(): number {
    const { infantSeats, toddlerSeats, boosterSeats } =
      this.form().getRawValue()
    return infantSeats + toddlerSeats + boosterSeats
  }

  /** As many of this kind as there is room left for (one per passenger, three per car). */
  protected seatMax(
    name: 'infantSeats' | 'toddlerSeats' | 'boosterSeats'
  ): number {
    const own = this.form().controls[name].value
    const room = childSeatLimit(this.passengers) - this.seats
    return Math.max(own, own + room)
  }

  /** Swaps the two addresses (and their meeting points). */
  protected swap(): void {
    const { pickup, dropoff } = this.form().getRawValue()
    this.form().patchValue({
      pickup: dropoff,
      dropoff: pickup,
      pickupMeetingPoint: null,
      dropoffMeetingPoint: null,
    })
  }

  protected submit(): void {
    this.submitted.set(true)
    this.peopleOpen.set(false)
    this.form().markAllAsTouched()
    if (this.form().valid) {
      this.searched.emit()
    }
  }

  protected closeOutside(event: Event): void {
    const people = this.people()?.nativeElement
    if (this.peopleOpen() && people && !people.contains(event.target as Node)) {
      this.peopleOpen.set(false)
    }
  }
}
