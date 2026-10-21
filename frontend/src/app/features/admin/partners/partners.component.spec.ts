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
})
