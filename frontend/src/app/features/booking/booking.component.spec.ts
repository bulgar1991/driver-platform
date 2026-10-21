import { Component, input } from '@angular/core'
import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { provideRouter } from '@angular/router'
import { Quote } from '@/core/models'
import { MapComponent } from '@/shared/components/map/map.component'
import { BookingComponent } from './booking.component'

/** Leaflet is not needed to test the form. */
@Component({ selector: 'app-map', template: '' })
class MapStubComponent {
  readonly markers = input<unknown>()
  readonly path = input<unknown>()
  readonly zones = input<unknown>()
  readonly fitPoints = input<unknown>()
  readonly locked = input<unknown>()
}

const quote = (): Quote => ({
  outbound: {
    pickup: { address: 'Roma Termini', latitude: 41.9, longitude: 12.5 },
    dropoff: { address: 'Fiumicino', latitude: 41.79, longitude: 12.25 },
    pickupAt: '2026-11-03T10:00:00',
    pickupAtUtc: '2026-11-03T09:00:00Z',
    pickupZone: null,
    dropoffZone: null,
    pickupMeetingPoint: null,
    dropoffMeetingPoint: null,
    distanceKm: 30,
    durationMinutes: 35,
    routeIsEstimate: false,
    path: [],
    surcharges: [],
    surchargePercent: 0,
  },
  return: null,
  vehicles: [
    {
      category: 'Sedan',
      maxPassengers: 3,
      maxLuggage: 3,
      available: true,
      unavailableReason: null,
      total: 58,
      vatIncluded: 5.27,
      outbound: {
        fare: 58,
        surchargePercent: 0,
        surchargeTotal: 0,
        discountTotal: 0,
        zoneFeesTotal: 0,
        extrasTotal: 0,
        total: 58,
        lines: [],
      },
      return: null,
    },
  ],
  currency: 'EUR',
  vatRatePercent: 10,
})

describe('BookingComponent', () => {
  let http: HttpTestingController

  beforeEach(() => {
    vi.useFakeTimers()
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    })
    TestBed.overrideComponent(BookingComponent, {
      remove: { imports: [MapComponent] },
      add: { imports: [MapStubComponent] },
    })
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows the approximate price as soon as both addresses are picked, without a button', () => {
    const fixture = TestBed.createComponent(BookingComponent)
    fixture.detectChanges()
    http.expectOne('/api/booking-options').flush({
      vehicles: [],
      extras: {},
      returnDiscountPercent: 0,
      minLeadTimeHours: 2,
      currency: 'EUR',
      surcharges: [],
    })
    expect(fixture.nativeElement.textContent).toContain('Enter where you start')

    const forms = (
      fixture.componentInstance as unknown as {
        forms: { trip: { patchValue(v: object): void } }
      }
    ).forms
    forms.trip.patchValue({
      pickup: { address: 'Roma Termini', latitude: 41.9, longitude: 12.5 },
      dropoff: { address: 'Fiumicino', latitude: 41.79, longitude: 12.25 },
    })
    vi.advanceTimersByTime(499)
    http.expectNone('/api/quotes')
    vi.advanceTimersByTime(1)

    http.expectOne({ method: 'POST', url: '/api/quotes' }).flush(quote())
    fixture.detectChanges()

    expect(
      fixture.nativeElement.querySelector('.vehicle-card__price').textContent
    ).toContain('€58.00')
    expect(fixture.nativeElement.textContent).toContain('30 km')
    http.verify()
  })
})
