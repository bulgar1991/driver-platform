import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  LOCALE_ID,
  OnInit,
  signal,
  viewChild,
} from '@angular/core'
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop'
import { FormBuilder } from '@angular/forms'
import { debounceTime, filter, merge } from 'rxjs'
import {
  LatLng,
  MapMarker,
  MapZone,
  QuoteLeg,
  VehicleCategory,
  Zone,
} from '@/core/models'
import { siteLocale } from '@/core/i18n/locales'
import { GeoService } from '@/core/services/geo.service'
import { MapComponent } from '@/shared/components/map/map.component'
import { DurationPipe } from '@/shared/pipes/duration.pipe'
import { DetailsStepComponent } from './components/details-step/details-step.component'
import { MeetingPointPickerComponent } from './components/meeting-point-picker/meeting-point-picker.component'
import { PriceSummaryComponent } from './components/price-summary/price-summary.component'
import { TripStepComponent } from './components/trip-step/trip-step.component'
import { VehicleCardComponent } from './components/vehicle-card/vehicle-card.component'
import {
  createBookingForms,
  toBookingRequest,
  toQuoteRequest,
} from './booking-form'
import { BookingStore } from './booking.store'

/** Delay after the last change before the price is recalculated. */
const QUOTE_DEBOUNCE_MS = 500

/**
 * The booking form, top to bottom: from / to, the map with the route, the approximate price of each
 * car, then options and contact once a car is chosen. Used on /book and on the home page.
 */
@Component({
  selector: 'app-booking',
  imports: [
    DetailsStepComponent,
    DurationPipe,
    MapComponent,
    MeetingPointPickerComponent,
    PriceSummaryComponent,
    TripStepComponent,
    VehicleCardComponent,
  ],
  providers: [BookingStore],
  templateUrl: './booking.component.html',
  styleUrl: './booking.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.booking--embedded]': 'embedded()' },
})
export class BookingComponent implements OnInit {
  /** On the home page the title is a section heading, not the page title. */
  readonly embedded = input(false)

  protected readonly store = inject(BookingStore)
  private readonly destroyRef = inject(DestroyRef)
  protected readonly forms = createBookingForms(inject(FormBuilder))
  private readonly detailsRef = viewChild<ElementRef<HTMLElement>>('details')
  private readonly geo = inject(GeoService)
  /** The site language: emails, messages and links for this booking use it. */
  private readonly language = siteLocale(inject(LOCALE_ID)).code

  protected readonly mapTargets = [
    { id: 'pickup', label: $localize`Set From` },
    { id: 'dropoff', label: $localize`Set To` },
  ] as const
  /** Which point a click on the map sets. */
  protected readonly mapTarget = signal<'pickup' | 'dropoff'>('pickup')
  protected readonly locating = signal(false)
  protected readonly mapError = signal<string | null>(null)

  private readonly trip = toSignal(this.forms.trip.valueChanges, {
    initialValue: this.forms.trip.value,
  })

  /** Both addresses picked: a price can be calculated. */
  protected readonly ready = computed(() => {
    const { pickup, dropoff } = this.trip()
    return !!pickup && !!dropoff
  })

  protected readonly markers = computed<MapMarker[]>(() => {
    const quote = this.store.quote()
    if (quote) {
      return legMarkers(quote.outbound)
    }
    const { pickup, dropoff } = this.trip()
    const markers: MapMarker[] = []
    if (pickup) {
      markers.push({
        position: [pickup.latitude, pickup.longitude],
        kind: 'pickup',
        label: pickup.address,
      })
    }
    if (dropoff) {
      markers.push({
        position: [dropoff.latitude, dropoff.longitude],
        kind: 'dropoff',
        label: dropoff.address,
      })
    }
    return markers
  })

  protected readonly path = computed<LatLng[]>(
    () => this.store.quote()?.outbound.path ?? []
  )

  protected readonly zones = computed<MapZone[]>(() => {
    const leg = this.store.quote()?.outbound
    return [leg?.pickupZone, leg?.dropoffZone]
      .filter((z): z is Zone => !!z)
      .map(zone => ({ zone, highlighted: zone.type === 'NoCarAccess' }))
  })

  protected readonly fitPoints = computed<LatLng[]>(() => [
    ...this.markers().map(m => m.position),
    ...this.path(),
  ])

  ngOnInit(): void {
    this.store.loadOptions()

    const { pickup, dropoff, pickupMeetingPoint, dropoffMeetingPoint } =
      this.forms.trip.controls
    // A new address invalidates the meeting point chosen for the old one.
    pickup.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => pickupMeetingPoint.setValue(null, { emitEvent: false }))
    dropoff.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => dropoffMeetingPoint.setValue(null, { emitEvent: false }))

    // The price follows the form: any change of route, time, people or options re-quotes.
    const {
      infantSeats,
      toddlerSeats,
      boosterSeats,
      nameSign,
      returnNameSign,
    } = this.forms.details.controls
    merge(
      this.forms.trip.valueChanges,
      infantSeats.valueChanges,
      toddlerSeats.valueChanges,
      boosterSeats.valueChanges,
      nameSign.valueChanges,
      returnNameSign.valueChanges
    )
      .pipe(
        debounceTime(QUOTE_DEBOUNCE_MS),
        filter(
          () =>
            this.ready() &&
            this.forms.trip.valid &&
            this.store.step() !== 'done'
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.requote())
  }

  /** A click on the map: the address there becomes From or To, then the price follows. */
  protected placeOnMap([latitude, longitude]: LatLng): void {
    const target = this.mapTarget()
    this.locating.set(true)
    this.mapError.set(null)
    this.geo.reverse(latitude, longitude).subscribe({
      next: place => {
        this.locating.set(false)
        const control = this.forms.trip.controls[target]
        control.setValue({
          address: place.address,
          latitude: place.latitude,
          longitude: place.longitude,
        })
        control.markAsTouched()
        // From is set: the next click sets To, unless it is already there.
        if (target === 'pickup' && !this.forms.trip.controls.dropoff.value) {
          this.mapTarget.set('dropoff')
        }
      },
      error: () => {
        this.locating.set(false)
        this.mapError.set(
          $localize`No address found there. Try a point on a street in Italy, France, Switzerland or Germany.`
        )
      },
    })
  }

  protected chooseMeetingPoint(
    direction: 'pickup' | 'dropoff',
    name: string
  ): void {
    const control =
      direction === 'pickup'
        ? this.forms.trip.controls.pickupMeetingPoint
        : this.forms.trip.controls.dropoffMeetingPoint
    control.setValue(name, { emitEvent: false })
    this.requote()
  }

  protected chooseVehicle(category: VehicleCategory): void {
    this.store.category.set(category)
    this.store.step.set('details')
    // The rest of the form opens below the prices; bring it into view.
    setTimeout(() =>
      this.detailsRef()?.nativeElement.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      })
    )
  }

  protected changeVehicle(): void {
    this.store.category.set(null)
    this.store.step.set('trip')
  }

  protected book(): void {
    const vehicle = this.store.vehicle()
    if (!vehicle) {
      return
    }
    const request = toBookingRequest(
      this.forms,
      vehicle.category,
      vehicle.total,
      this.language
    )
    this.store.book(request).subscribe({
      next: result => {
        if (result.paymentUrl) {
          // Stripe's hosted page; it sends the client back to their booking page.
          window.location.assign(result.paymentUrl)
        } else {
          // Nothing to pay (a partner booking): the client's page, in the client's language.
          window.location.assign(result.manageUrl)
        }
      },
      error: () => undefined,
    })
  }

  private requote(): void {
    this.store.requestQuote(toQuoteRequest(this.forms)).subscribe({
      next: () => {
        // The chosen car no longer fits (more passengers…): back to choosing.
        if (this.store.step() === 'details' && !this.store.vehicle()) {
          this.store.step.set('trip')
        }
      },
      error: () => undefined,
    })
  }
}

function legMarkers(leg: QuoteLeg): MapMarker[] {
  const markers: MapMarker[] = []
  const add = (
    point: { latitude: number; longitude: number },
    kind: MapMarker['kind'],
    label: string
  ) =>
    markers.push({ position: [point.latitude, point.longitude], kind, label })

  if (leg.pickupMeetingPoint) {
    add(
      leg.pickupMeetingPoint,
      'pickup',
      `Pickup: ${leg.pickupMeetingPoint.name}`
    )
    add(leg.pickup, 'meeting', leg.pickup.address)
  } else {
    add(leg.pickup, 'pickup', leg.pickup.address)
  }
  if (leg.dropoffMeetingPoint) {
    add(
      leg.dropoffMeetingPoint,
      'dropoff',
      `Drop-off: ${leg.dropoffMeetingPoint.name}`
    )
    add(leg.dropoff, 'meeting', leg.dropoff.address)
  } else {
    add(leg.dropoff, 'dropoff', leg.dropoff.address)
  }
  return markers
}
