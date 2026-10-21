import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { ReviewComponent } from './review.component'

describe('ReviewComponent', () => {
  let http: HttpTestingController

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => http.verify())

  it('sends the chosen stars and comment once', () => {
    const fixture = TestBed.createComponent(ReviewComponent)
    fixture.componentRef.setInput('token', 'rev')
    fixture.detectChanges()
    http.expectOne('/api/reviews/rev').flush({
      reference: 'MD-AAA222',
      pickupAt: '2026-11-03T09:00:00',
      from: 'Piazzale Roma',
      to: 'Marco Polo',
      driverFirstName: 'Marco',
      submitted: false,
    })
    fixture.detectChanges()

    const stars: HTMLButtonElement[] = [
      ...fixture.nativeElement.querySelectorAll('.review__star'),
    ]
    stars[3].click()
    fixture.detectChanges()
    const send = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Send review')
    )
    send.click()

    const request = http.expectOne({ method: 'POST', url: '/api/reviews/rev' })
    expect(request.request.body).toEqual({ rating: 4, comment: '' })
    request.flush(null, { status: 204, statusText: 'No Content' })
    fixture.detectChanges()
    expect(fixture.nativeElement.textContent).toContain('Thank you')
  })
})
