import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { provideRouter } from '@angular/router'
import { MyBooking } from '@/core/models'
import { MyBookingComponent } from './my-booking.component'

const booking = (status: MyBooking['status']): MyBooking => ({
  reference: 'MD-ABC234',
  status,
  vehicleCategory: 'Sedan',
  clientName: 'Maria Rossi',
  legs: [],
  total: 58,
  vatRatePercent: 10,
  currency: 'EUR',
  canPay: status === 'AwaitingPayment',
  payment: null,
  invoices: [],
})

describe('MyBookingComponent', () => {
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
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => {
    http.verify()
    vi.useRealTimers()
  })

  function render(paid?: string) {
    const fixture = TestBed.createComponent(MyBookingComponent)
    fixture.componentRef.setInput('reference', 'MD-ABC234')
    fixture.componentRef.setInput('token', 'tok')
    fixture.componentRef.setInput('paid', paid)
    fixture.detectChanges()
    return fixture
  }

  it('waits for the webhook after returning from the payment page', () => {
    const fixture = render('1')
    http
      .expectOne('/api/my-booking/MD-ABC234/tok')
      .flush(booking('AwaitingPayment'))
    fixture.detectChanges()
    expect(fixture.nativeElement.textContent).toContain(
      'Confirming your payment'
    )

    vi.advanceTimersByTime(2000)
    http.expectOne('/api/my-booking/MD-ABC234/tok').flush(booking('New'))
    fixture.detectChanges()

    expect(fixture.nativeElement.textContent).toContain('Confirmed')
    expect(fixture.nativeElement.textContent).not.toContain(
      'Confirming your payment'
    )
    vi.advanceTimersByTime(10_000)
    http.expectNone('/api/my-booking/MD-ABC234/tok')
  })

  it('offers to pay when the booking is still unpaid', () => {
    const fixture = render()
    http
      .expectOne('/api/my-booking/MD-ABC234/tok')
      .flush(booking('AwaitingPayment'))
    fixture.detectChanges()

    expect(
      fixture.nativeElement.querySelector('.my-booking__pay button').textContent
    ).toContain('Pay €58.00')
  })

  it('says so when the link is wrong', () => {
    const fixture = render()
    http
      .expectOne('/api/my-booking/MD-ABC234/tok')
      .flush(
        { title: 'Booking not found.' },
        { status: 404, statusText: 'Not Found' }
      )
    fixture.detectChanges()

    expect(fixture.nativeElement.textContent).toContain('Booking not found')
  })
})
