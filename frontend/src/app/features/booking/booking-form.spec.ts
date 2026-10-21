import { TestBed } from '@angular/core/testing'
import { FormBuilder } from '@angular/forms'
import {
  createBookingForms,
  toBookingRequest,
  toQuoteRequest,
} from './booking-form'

const rome = { address: 'Roma Termini', latitude: 41.901, longitude: 12.5016 }
const fiumicino = {
  address: 'Fiumicino',
  latitude: 41.7934,
  longitude: 12.2522,
}

describe('booking form', () => {
  const create = () => createBookingForms(TestBed.inject(FormBuilder))

  it('builds the quote request in Italian local time with child seats', () => {
    const forms = create()
    forms.trip.patchValue({
      pickup: rome,
      dropoff: fiumicino,
      date: '2026-11-03',
      time: '07:15',
    })
    forms.details.patchValue({
      toddlerSeats: 1,
      boosterSeats: 2,
      nameSign: true,
    })

    const request = toQuoteRequest(forms)

    expect(request.pickupAt).toBe('2026-11-03T07:15:00')
    expect(request.childSeats).toEqual([
      { group: 'Toddler', count: 1 },
      { group: 'Booster', count: 2 },
    ])
    expect(request.nameSign).toBe(true)
    expect(request.return).toBeNull()
  })

  it('requires the return date and time only for a return trip', () => {
    const forms = create()
    forms.trip.patchValue({ pickup: rome, dropoff: fiumicino })
    expect(forms.trip.valid).toBe(true)

    forms.trip.patchValue({ returnTrip: true })
    expect(forms.trip.hasError('returnRequired')).toBe(true)

    forms.trip.patchValue({ returnDate: '2026-11-10', returnTime: '18:00' })
    expect(forms.trip.valid).toBe(true)
    expect(toQuoteRequest(forms).return?.pickupAt).toBe('2026-11-10T18:00:00')
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
})
