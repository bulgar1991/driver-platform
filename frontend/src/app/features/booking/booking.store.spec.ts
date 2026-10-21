import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import {
  CreateBookingRequest,
  Quote,
  QuoteRequest,
  VehicleOption,
} from '@/core/models'
import { BookingStore } from './booking.store'

const leg = {
  pickup: { address: 'A', latitude: 41.9, longitude: 12.5 },
  dropoff: { address: 'B', latitude: 41.8, longitude: 12.4 },
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
}

function vehicle(
  category: VehicleOption['category'],
  total: number,
  available = true
): VehicleOption {
  const price = {
    fare: total,
    surchargePercent: 0,
    surchargeTotal: 0,
    discountTotal: 0,
    zoneFeesTotal: 0,
    extrasTotal: 0,
    total,
    lines: [],
  }
  return {
    category,
    maxPassengers: 3,
    maxLuggage: 3,
    available,
    unavailableReason: available ? null : 'Up to 3 passengers',
    total,
    vatIncluded: 0,
    outbound: price,
    return: null,
  }
}

const quote = (sedanTotal: number, sedanAvailable = true): Quote => ({
  outbound: leg,
  return: null,
  vehicles: [vehicle('Sedan', sedanTotal, sedanAvailable), vehicle('Van', 81)],
  currency: 'EUR',
  vatRatePercent: 10,
})

describe('BookingStore', () => {
  let store: BookingStore
  let http: HttpTestingController

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        BookingStore,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    })
    store = TestBed.inject(BookingStore)
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => http.verify())

  it('drops the chosen vehicle when it no longer fits the trip', () => {
    store.category.set('Sedan')
    store.requestQuote({} as QuoteRequest).subscribe()
    http.expectOne('/api/quotes').flush(quote(58, false))

    expect(store.category()).toBeNull()
    expect(store.vehicle()).toBeNull()
  })

  it('shows the new price when the API answers 409', () => {
    store.category.set('Sedan')
    store.quote.set(quote(58))

    store.book({} as CreateBookingRequest).subscribe({ error: () => undefined })
    http
      .expectOne('/api/bookings')
      .flush(
        { title: 'The price has changed.', data: quote(63.8) },
        { status: 409, statusText: 'Conflict' }
      )

    expect(store.priceChanged()).toBe(true)
    expect(store.error()).toBe('The price has changed.')
    expect(store.vehicle()?.total).toBe(63.8)
    expect(store.step()).toBe('trip')
  })

  it('moves to the confirmation once booked', () => {
    store.book({} as CreateBookingRequest).subscribe()
    http.expectOne('/api/bookings').flush({ reference: 'MD-ABC234', legs: [] })

    expect(store.step()).toBe('done')
    expect(store.result()?.reference).toBe('MD-ABC234')
  })
})
