import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { DeferBlockBehavior, TestBed } from '@angular/core/testing'
import { ActivatedRoute, provideRouter } from '@angular/router'
import { Quote } from '@/core/models'
import { BookingComponent } from './booking.component'

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
  // Injected only once a test has set its providers (one replaces the route).
  let http: HttpTestingController

  beforeEach(async () => {
    TestBed.configureTestingModule({
      // The map (Leaflet) is deferred; it is not needed to test the form, so it never loads.
      deferBlockBehavior: DeferBlockBehavior.Manual,
      imports: [BookingComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    })
    await TestBed.compileComponents()
    vi.useFakeTimers()
  })

  const create = () => {
    const fixture = TestBed.createComponent(BookingComponent)
    http = TestBed.inject(HttpTestingController)
    return fixture
  }

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows the approximate price as soon as both addresses are picked, without a button', () => {
    const fixture = create()
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

  it('fills the search in from a link from the home page and prices it', () => {
    TestBed.overrideProvider(ActivatedRoute, {
      useValue: {
        snapshot: {
          queryParams: {
            from: 'Roma Termini',
            fromLat: '41.9',
            fromLng: '12.5',
            to: 'Fiumicino',
            toLat: '41.79',
            toLng: '12.25',
            date: '2026-12-01',
            time: '09:30',
            pax: '3',
          },
        },
      },
    })
    const fixture = create()
    fixture.detectChanges()
    http.expectOne('/api/booking-options')
    vi.advanceTimersByTime(500)

    const request = http.expectOne({ method: 'POST', url: '/api/quotes' })
      .request.body as { pickupAt: string; passengers: number }
    expect(request.pickupAt).toBe('2026-12-01T09:30:00')
    expect(request.passengers).toBe(3)
  })
})
