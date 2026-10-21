import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { provideRouter } from '@angular/router'
import { DriversJoinComponent } from './drivers-join.component'

describe('DriversJoinComponent', () => {
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

  function type(
    fixture: { nativeElement: HTMLElement },
    selector: string,
    value: string
  ) {
    const input =
      fixture.nativeElement.querySelector<HTMLInputElement>(selector)!
    input.value = value
    input.dispatchEvent(new Event('input'))
  }

  it('sends the application and thanks the driver', () => {
    const fixture = TestBed.createComponent(DriversJoinComponent)
    fixture.detectChanges()
    const el: HTMLElement = fixture.nativeElement
    type(fixture, '#djFirst', 'Paolo')
    type(fixture, '#djLast', 'Russo')
    type(fixture, '#djPhone', '+39 333 000 1111')
    type(fixture, '#djEmail', 'paolo@example.com')
    type(fixture, '#djCity', 'Venezia')
    el.querySelectorAll<HTMLButtonElement>('.drivers-join__chip')[1].click() // English
    el.querySelector<HTMLInputElement>('.drivers-join__consent input')!.click()
    fixture.detectChanges()

    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click()
    const request = http.expectOne({
      method: 'POST',
      url: '/api/driver-applications',
    })
    expect(request.request.body).toMatchObject({
      firstName: 'Paolo',
      licence: 'Ncc',
      languages: ['it', 'en'],
      hasOwnVehicle: false,
      vehicle: null,
      privacyConsent: true,
      website: '',
    })
    request.flush(null, { status: 202, statusText: 'Accepted' })
    fixture.detectChanges()

    expect(el.textContent).toContain('Thank you, Paolo!')
  })

  it('does not send without the privacy consent', () => {
    const fixture = TestBed.createComponent(DriversJoinComponent)
    fixture.detectChanges()

    fixture.nativeElement.querySelector('button[type=submit]').click()
    fixture.detectChanges()

    http.expectNone('/api/driver-applications')
    expect(fixture.nativeElement.textContent).toContain(
      'Please accept to continue.'
    )
  })
})
