import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { DriverPortal } from '@/core/models'
import { DriverPortalComponent } from './driver-portal.component'

const portal = (status: 'Seen' | 'Confirmed'): DriverPortal => ({
  driverFirstName: 'Marco',
  shifts: [
    {
      id: 's1',
      status,
      shiftDate: '2026-11-03',
      startsAt: '2026-11-03T09:00:00',
      endsAt: '2026-11-03T09:20:00',
      vehicleLabel: 'Black Mercedes E-Class',
      vehiclePlate: 'GA123BC',
      notes: null,
      canConfirm: status === 'Seen',
      bookings: [],
    },
  ],
})

describe('DriverPortalComponent', () => {
  let http: HttpTestingController

  beforeEach(() => {
    // The page polls with rxjs timer(0, 60 s): fake timers make the first load synchronous.
    vi.useFakeTimers()
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => {
    http.verify()
    vi.useRealTimers()
  })

  function render() {
    const fixture = TestBed.createComponent(DriverPortalComponent)
    fixture.componentRef.setInput('token', 'tok')
    fixture.detectChanges()
    vi.advanceTimersByTime(0)
    return fixture
  }

  it('lets the driver confirm a shift', () => {
    const fixture = render()
    http.expectOne('/api/driver/tok').flush(portal('Seen'))
    fixture.detectChanges()

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.driver-portal__shift button'
    )
    expect(button.textContent).toContain('Confirm this shift')
    button.click()
    http
      .expectOne({ method: 'POST', url: '/api/driver/tok/shifts/s1/confirm' })
      .flush(portal('Confirmed'))
    fixture.detectChanges()

    expect(fixture.nativeElement.textContent).not.toContain(
      'Confirm this shift'
    )
    expect(fixture.nativeElement.textContent).toContain('Hi Marco')
  })

  it('explains an invalid link', () => {
    const fixture = render()
    http
      .expectOne('/api/driver/tok')
      .flush({ title: 'Not valid' }, { status: 404, statusText: 'Not Found' })
    fixture.detectChanges()

    expect(fixture.nativeElement.textContent).toContain(
      'This link is not valid'
    )
  })
})
