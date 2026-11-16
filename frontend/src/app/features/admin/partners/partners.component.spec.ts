import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { Partner } from '@/core/models'
import { PartnersComponent } from './partners.component'

const partner: Partner = {
  id: 'p1',
  name: 'Hotel Danieli',
  kind: 'Company',
  contactEmail: 'concierge@hotel.test',
  priceAdjustmentPercent: -10,
  commissionPercent: 5,
  monthlyInvoicing: true,
  notifyPassengers: true,
  webhookUrl: null,
  isActive: true,
  apiKeyPrefix: null,
  apiKeyCreatedAt: null,
  apiKeyLastUsedAt: null,
  bookingsThisMonth: 0,
  webhooksFailing: 0,
}

describe('PartnersComponent', () => {
  let http: HttpTestingController

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => http.verify())

  it('shows a new API key once, right after creating it', () => {
    const fixture = TestBed.createComponent(PartnersComponent)
    fixture.detectChanges()
    http.expectOne('/api/admin/partners').flush([partner])
    fixture.detectChanges()

    fixture.nativeElement.querySelector('.partners__row').click()
    fixture.detectChanges()
    const create = [...document.querySelectorAll('button')].find(b =>
      b.textContent?.includes('Create API key')
    )!
    create.click()
    http
      .expectOne({ method: 'POST', url: '/api/admin/partners/p1/api-key' })
      .flush({ apiKey: 'md_abcdefgh_secret', prefix: 'abcdefgh' })
    http
      .expectOne('/api/admin/partners')
      .flush([{ ...partner, apiKeyPrefix: 'abcdefgh' }])
    fixture.detectChanges()

    const input = document.querySelector<HTMLInputElement>(
      'input[aria-label="New API key"]'
    )!
    expect(input.value).toBe('md_abcdefgh_secret')
    expect(document.body.textContent).toContain('shown only once')
  })

  function syncButton(fixture: { nativeElement: HTMLElement }) {
    return fixture.nativeElement.querySelector<HTMLButtonElement>(
      '.partners__sync'
    )!
  }

  it('syncs Booking.com on demand and shows what changed', () => {
    const fixture = TestBed.createComponent(PartnersComponent)
    fixture.detectChanges()
    http.expectOne('/api/admin/partners').flush([])
    fixture.detectChanges()

    syncButton(fixture).click()
    fixture.detectChanges()
    expect(syncButton(fixture).disabled).toBe(true)
    expect(syncButton(fixture).textContent).toContain('Syncing')

    http
      .expectOne({ method: 'POST', url: '/api/admin/booking-com/sync' })
      .flush({
        fetched: 22,
        imported: 3,
        amended: 1,
        cancelled: 0,
        accepted: 3,
        rejected: 0,
        driversSent: 0,
        eventsSent: 0,
        errors: 0,
      })
    // The first ride creates the Booking.com partner, so the list is reloaded.
    http
      .expectOne('/api/admin/partners')
      .flush([{ ...partner, id: 'b1', name: 'Booking.com', kind: 'Booking' }])
    fixture.detectChanges()

    const status = fixture.nativeElement.querySelector('[role="status"]')
    expect(status.textContent).toContain('22 rides checked, 3 new, 1 changed')
    expect(status.classList).toContain('alert--info')
    expect(syncButton(fixture).disabled).toBe(false)
  })

  it('says why a Booking.com sync failed', () => {
    const fixture = TestBed.createComponent(PartnersComponent)
    fixture.detectChanges()
    http.expectOne('/api/admin/partners').flush([])
    fixture.detectChanges()

    syncButton(fixture).click()
    http.expectOne('/api/admin/booking-com/sync').flush(
      {
        title: 'BookingCom:ClientId and BookingCom:ClientSecret are not set.',
      },
      { status: 502, statusText: 'Bad Gateway' }
    )
    fixture.detectChanges()

    const status = fixture.nativeElement.querySelector('[role="status"]')
    expect(status.textContent).toContain(
      'Booking.com sync failed: BookingCom:ClientId'
    )
    expect(status.classList).toContain('alert--error')
  })
})
