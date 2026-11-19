import { TestBed } from '@angular/core/testing'
import { FormBuilder } from '@angular/forms'
import { nowIn, OFFICE_TIME_ZONE, todayInOffice } from '@/core/utils/local-time'
import {
  canQuote,
  createBookingForms,
  toBookingRequest,
  toQuoteRequest,
  tripFromParams,
  tripToParams,
} from './booking-form'

const rome = { address: 'Roma Termini', latitude: 41.901, longitude: 12.5016 }
const fiumicino = {
  address: 'Fiumicino',
  latitude: 41.7934,
  longitude: 12.2522,
}

describe('booking form', () => {
  const create = () => createBookingForms(TestBed.inject(FormBuilder))

  it('builds the quote request in local time with child seats', () => {
    const forms = create()
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      date: '2026-11-03',
      time: '07:15',
      passengers: 3,
      toddlerSeats: 1,
      boosterSeats: 2,
    })
    forms.details.patchValue({ nameSign: true })

    const request = toQuoteRequest(forms)

    expect(request.pickupAt).toBe('2026-11-03T07:15:00')
    expect(request.childSeats).toEqual([
      { group: 'Toddler', count: 1 },
      { group: 'Booster', count: 2 },
    ])
    expect(request.nameSign).toBe(true)
    expect(request.return).toBeNull()
  })

  it('starts with no passengers; prices can show, booking needs at least one', () => {
    const forms = create()
    forms.trip.patchValue({ pickup: rome, dropoff: fiumicino })
    expect(forms.trip.controls.passengers.value).toBe(0)
    expect(forms.trip.valid).toBe(false)
    expect(canQuote(forms.trip)).toBe(true)
    expect(toQuoteRequest(forms).passengers).toBe(1)

    forms.trip.patchValue({ passengers: 3 })
    expect(forms.trip.valid).toBe(true)
    expect(toQuoteRequest(forms).passengers).toBe(3)
  })

  it('requires the return date and time only for a return trip', () => {
    const forms = create()
    forms.trip.patchValue({ pickup: rome, dropoff: fiumicino, passengers: 2 })
    expect(forms.trip.valid).toBe(true)

    forms.trip.patchValue({ returnTrip: true })
    expect(forms.trip.hasError('returnRequired')).toBe(true)

    // After the outbound trip (tomorrow by default), whatever today is.
    const returnDate = todayInOffice(8)
    forms.trip.patchValue({ returnDate, returnTime: '18:00' })
    expect(forms.trip.valid).toBe(true)
    expect(toQuoteRequest(forms).return?.pickupAt).toBe(
      `${returnDate}T18:00:00`
    )
  })

  it('needs privacy consent and trims the contact details', () => {
    const forms = create()
    forms.trip.patchValue({ pickup: rome, dropoff: fiumicino })
    forms.contact.patchValue({
      firstName: ' Maria ',
      lastName: 'Rossi',
      email: 'maria@example.com',
      phone: '+39 333 123 4567',
    })
    expect(forms.contact.valid).toBe(false)

    forms.contact.patchValue({ gdprConsent: true })
    const request = toBookingRequest(forms, 'Sedan', 58, 'it')

    expect(forms.contact.valid).toBe(true)
    expect(request.contact.firstName).toBe('Maria')
    expect(request.contact.language).toBe('it')
    expect(request.expectedTotal).toBe(58)
    expect(request.nameSignText).toBeNull()
  })

  it('sends takeoff and landing for each leg, only with a flight number', () => {
    const forms = create()
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      returnTrip: true,
      returnDate: '2026-11-10',
      returnTime: '18:00',
    })
    forms.details.patchValue({
      flightNumber: ' fr 1234 ',
      flightDepartsAt: '2026-11-03T10:40',
      flightLandsAt: '2026-11-03T12:55',
      returnFlightLandsAt: '2026-11-10T17:30',
    })

    const request = toBookingRequest(forms, 'Sedan', null, 'it')

    expect(request.flightNumber).toBe('fr 1234')
    expect(request.flightDepartsAt).toBe('2026-11-03T10:40:00')
    expect(request.flightLandsAt).toBe('2026-11-03T12:55:00')
    // No return flight number: its times are not sent.
    expect(request.trip.return?.flightLandsAt).toBeNull()

    forms.details.patchValue({ returnFlightNumber: 'BA2584' })
    expect(toQuoteRequest(forms).return?.flightLandsAt).toBe(
      '2026-11-10T17:30:00'
    )
  })

  it('refuses a pickup sooner than the minimum notice, as the API would', () => {
    const forms = createBookingForms(TestBed.inject(FormBuilder), () => 3)
    const [date, time] = nowIn(OFFICE_TIME_ZONE, 60).split('T')
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      date,
      time,
      passengers: 2,
    })
    expect(forms.trip.getError('tooSoon')).toEqual({ hours: 3 })

    forms.trip.patchValue({ date: todayInOffice(2) })
    expect(forms.trip.valid).toBe(true)
  })

  it('counts the minimum notice in the local time at the pickup', () => {
    let zone = 'Europe/London'
    const forms = createBookingForms(
      TestBed.inject(FormBuilder),
      () => 1,
      () => zone
    )
    // 90 minutes from now on a London clock: enough notice for a London pickup…
    const [date, time] = nowIn('Europe/London', 90).split('T')
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      date,
      time,
      passengers: 2,
    })
    expect(forms.trip.hasError('tooSoon')).toBe(false)

    // …but already past on an Athens clock (two hours ahead of London).
    zone = 'Europe/Athens'
    forms.trip.updateValueAndValidity()
    expect(forms.trip.getError('tooSoon')).toEqual({ hours: 1 })
  })

  it('refuses a return before the outbound trip', () => {
    const forms = create()
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      date: todayInOffice(5),
      time: '10:00',
      returnTrip: true,
      returnDate: todayInOffice(5),
      returnTime: '09:00',
    })
    expect(forms.trip.hasError('returnBeforeOutbound')).toBe(true)
  })

  it('carries the search in a link and reads it back, ignoring what is malformed', () => {
    const forms = create()
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      date: '2026-11-03',
      time: '07:15',
      passengers: 3,
      largeLuggage: 1,
      boosterSeats: 1,
      returnTrip: true,
      returnDate: '2026-11-10',
      returnTime: '18:00',
    })

    expect(tripFromParams(tripToParams(forms.trip))).toEqual({
      pickup: rome,
      dropoff: fiumicino,
      date: '2026-11-03',
      time: '07:15',
      passengers: 3,
      largeLuggage: 1,
      smallLuggage: 0,
      boosterSeats: 1,
      returnTrip: true,
      returnDate: '2026-11-10',
      returnTime: '18:00',
    })
    expect(
      tripFromParams({ from: 'Roma', fromLat: 'x', pax: '40', date: '3 nov' })
    ).toEqual({})
  })

  it('allows one child seat per passenger and three per car, as the API', () => {
    const forms = create()
    forms.trip.patchValue({ passengers: 2, infantSeats: 1, boosterSeats: 1 })
    expect(forms.trip.hasError('childSeats')).toBe(false)

    forms.trip.patchValue({ passengers: 1 })
    expect(forms.trip.getError('childSeats')).toEqual({ max: 1 })

    forms.trip.patchValue({ passengers: 6, toddlerSeats: 2 })
    expect(forms.trip.getError('childSeats')).toEqual({ max: 3 })
  })
})
