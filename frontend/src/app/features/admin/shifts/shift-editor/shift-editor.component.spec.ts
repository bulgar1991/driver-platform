import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { Availability, BookingListItem } from '@/core/models'
import { ShiftEditorComponent } from './shift-editor.component'

const booking: BookingListItem = {
  id: 'b1',
  reference: 'MD-AAA222',
  status: 'New',
  source: 'Website',
  pickupAt: '2026-11-03T09:00:00',
  pickupLabel: 'Piazzale Roma',
  dropoffLabel: 'Marco Polo',
  clientName: 'Maria Rossi',
  clientPhone: '+39333',
  passengers: 2,
  largeLuggage: 2,
  smallLuggage: 0,
  childSeats: 0,
  vehicleCategory: 'Sedan',
  distanceKm: 13.5,
  durationMinutes: 18,
  totalPrice: 44.76,
  flightNumber: null,
  assignmentId: null,
  driverName: null,
  vehiclePlate: null,
  isReturnLeg: false,
  partnerName: null,
}

const availability: Availability = {
  drivers: [
    { id: 'd2', name: 'Giulia Ferrari', available: true, problems: [] },
    {
      id: 'd1',
      name: 'Marco Bianchi',
      available: false,
      problems: ['Already on another shift at this time'],
    },
  ],
  vehicles: [
    {
      id: 'v1',
      label: 'Mercedes E-Class',
      plateNumber: 'GA123BC',
      category: 'Sedan',
      available: true,
      problems: [],
    },
  ],
}

describe('ShiftEditorComponent', () => {
  let http: HttpTestingController

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => http.verify())

  it('suggests the first free driver and car, then saves the shift', () => {
    const fixture = TestBed.createComponent(ShiftEditorComponent)
    fixture.componentRef.setInput('candidates', [booking])
    fixture.componentRef.setInput('preselected', ['b1'])
    fixture.detectChanges()

    http
      .expectOne(
        r =>
          r.url === '/api/admin/shifts/availability' &&
          r.params.get('bookingIds') === 'b1'
      )
      .flush(availability)
    fixture.detectChanges()

    const saved = vi.fn()
    fixture.componentInstance.saved.subscribe(saved)
    const save = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Create shift')
    )
    save.click()

    const request = http.expectOne('/api/admin/shifts')
    expect(request.request.body).toEqual({
      driverId: 'd2',
      vehicleId: 'v1',
      bookingIds: ['b1'],
      notes: null,
    })
    request.flush({ shift: { id: 's1' }, warnings: [] })
    expect(saved).toHaveBeenCalled()
  })

  it('shows why a driver is not available', async () => {
    const fixture = TestBed.createComponent(ShiftEditorComponent)
    fixture.componentRef.setInput('candidates', [booking])
    fixture.componentRef.setInput('preselected', ['b1'])
    fixture.detectChanges()
    http
      .expectOne(r => r.url === '/api/admin/shifts/availability')
      .flush(availability)
    fixture.detectChanges()
    await fixture.whenStable()

    const select: HTMLSelectElement =
      fixture.nativeElement.querySelector('select')
    select.value = 'd1'
    select.dispatchEvent(new Event('change'))
    fixture.detectChanges()

    expect(fixture.nativeElement.textContent).toContain(
      'Already on another shift at this time'
    )
  })
})
