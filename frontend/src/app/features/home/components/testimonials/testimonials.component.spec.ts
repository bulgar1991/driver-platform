import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { provideRouter } from '@angular/router'
import { Testimonial } from '@/core/models'
import { TestimonialsComponent } from './testimonials.component'

const review = (author: string): Testimonial => ({
  author,
  rating: 5,
  comment: `Great ride with ${author}`,
  date: '2026-10-01',
  vehicleCategory: 'Sedan',
})

describe('TestimonialsComponent', () => {
  let http: HttpTestingController

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    })
    http = TestBed.inject(HttpTestingController)
  })

  afterEach(() => http.verify())

  it('slides with the arrows and wraps around', () => {
    // Phone width: one review per view.
    Object.defineProperty(window, 'innerWidth', {
      configurable: true,
      value: 390,
    })
    const fixture = TestBed.createComponent(TestimonialsComponent)
    fixture.detectChanges()
    http
      .expectOne('/api/testimonials')
      .flush([review('Maria R.'), review('John S.'), review('Anna M.')])
    fixture.detectChanges()

    const track: HTMLElement = fixture.nativeElement.querySelector(
      '.testimonials__track'
    )
    const next: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[aria-label="Next review"]'
    )
    const prev: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[aria-label="Previous review"]'
    )
    expect(track.style.transform).toContain('calc(0%')

    next.click()
    fixture.detectChanges()
    expect(track.style.transform).toContain('-100%')

    prev.click()
    prev.click()
    fixture.detectChanges()
    // From the first slide, "previous" goes to the last one.
    expect(track.style.transform).toContain('-200%')
  })

  it('invites to book instead of showing made-up reviews', () => {
    const fixture = TestBed.createComponent(TestimonialsComponent)
    fixture.detectChanges()
    http.expectOne('/api/testimonials').flush([])
    fixture.detectChanges()

    expect(fixture.nativeElement.textContent).toContain(
      'Your review could be the first here'
    )
    expect(
      fixture.nativeElement.querySelector('.testimonials__track')
    ).toBeNull()
  })
})
