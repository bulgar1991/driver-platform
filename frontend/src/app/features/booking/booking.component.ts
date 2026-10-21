import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
} from '@angular/core'
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop'
import { FormBuilder } from '@angular/forms'
import { Router } from '@angular/router'
import { debounceTime, filter, merge } from 'rxjs'
import {
  LatLng,
  MapMarker,
  MapZone,
  QuoteLeg,
  VehicleCategory,
  Zone,
} from '@/core/models'
import { MapComponent } from '@/shared/components/map/map.component'
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
import { BookingStep, BookingStore } from './booking.store'

interface StepItem {
  id: Exclude<BookingStep, 'done'>
  label: string
}

const STEPS: StepItem[] = [
  { id: 'trip', label: 'Trip' },
  { id: 'vehicle', label: 'Vehicle' },
  { id: 'details', label: 'Details' },
]

@Component({
  selector: 'app-booking',
  imports: [
    DetailsStepComponent,
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
})
export class BookingComponent implements OnInit {
  protected readonly store = inject(BookingStore)
  private readonly router = inject(Router)
  private readonly destroyRef = inject(DestroyRef)
  protected readonly forms = createBookingForms(inject(FormBuilder))
  protected readonly steps = STEPS

  private readonly trip = toSignal(this.forms.trip.valueChanges, {
    initialValue: this.forms.trip.value,
  })

  protected readonly stepIndex = computed(() =>
    STEPS.findIndex(s => s.id === this.store.step())
  )

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

    // A new address invalidates the meeting point chosen for the old one.
    const { pickup, dropoff, pickupMeetingPoint, dropoffMeetingPoint } =
      this.forms.trip.controls
    pickup.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => pickupMeetingPoint.setValue(null))
    dropoff.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => dropoffMeetingPoint.setValue(null))

    // Child seats and name signs change the price: re-quote while on the details step.
    const {
      infantSeats,
      toddlerSeats,
      boosterSeats,
      nameSign,
      returnNameSign,
    } = this.forms.details.controls
    merge(
      infantSeats.valueChanges,
      toddlerSeats.valueChanges,
      boosterSeats.valueChanges,
      nameSign.valueChanges,
      returnNameSign.valueChanges
    )
      .pipe(
        debounceTime(400),
        filter(() => this.store.step() === 'details'),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.requote())
  }

  protected searchPrices(): void {
    this.store.requestQuote(toQuoteRequest(this.forms)).subscribe({
      next: () => this.goTo('vehicle'),
      error: () => undefined,
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
    this.goTo('details')
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
      browserLanguage()
    )
    this.store.book(request).subscribe({
      next: result => {
        if (result.paymentUrl) {
          // Stripe's hosted page; it sends the client back to their booking page.
          window.location.assign(result.paymentUrl)
        } else {
          void this.router.navigateByUrl(new URL(result.manageUrl).pathname)
        }
      },
      error: () => undefined,
    })
  }

  protected goTo(step: BookingStep): void {
    this.store.step.set(step)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  private requote(): void {
    this.store.requestQuote(toQuoteRequest(this.forms)).subscribe({
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

function browserLanguage(): string {
  const lang = (navigator.language ?? 'en').slice(0, 2).toLowerCase()
  return /^[a-z]{2}$/.test(lang) ? lang : 'en'
}
