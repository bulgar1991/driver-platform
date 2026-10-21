import { provideHttpClient } from '@angular/common/http'
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing'
import { TestBed } from '@angular/core/testing'
import { provideRouter, Router } from '@angular/router'
import { LoginComponent } from './login.component'

describe('LoginComponent', () => {
  function render() {
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    })
    const fixture = TestBed.createComponent(LoginComponent)
    fixture.detectChanges()
    const el = fixture.nativeElement as HTMLElement
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!
      input.value = value
      input.dispatchEvent(new Event('input'))
    }
    const submit = () => {
      el.querySelector('form')!.dispatchEvent(new Event('submit'))
      fixture.detectChanges()
    }
    return { fixture, el, type, submit }
  }

  it('shows field errors instead of calling the API when empty', () => {
    const { el, submit } = render()
    submit()

    expect(el.querySelectorAll('.form-field__error').length).toBe(2)
    TestBed.inject(HttpTestingController).expectNone('/api/auth/login')
  })

  it('shows the API error on wrong credentials', () => {
    const { fixture, el, type, submit } = render()
    type('#email', 'admin@test.local')
    type('#password', 'wrong')
    submit()

    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' })
    fixture.detectChanges()

    expect(el.querySelector('.alert--error')?.textContent).toContain(
      'Invalid email or password'
    )
  })

  it('goes to the dashboard after signing in', () => {
    const { type, submit } = render()
    const navigate = vi
      .spyOn(TestBed.inject(Router), 'navigateByUrl')
      .mockResolvedValue(true)
    type('#email', 'admin@test.local')
    type('#password', 'right')
    submit()

    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/login')
      .flush({
        accessToken: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: {
          id: '1',
          email: 'admin@test.local',
          fullName: 'A',
          role: 'Admin',
        },
      })

    expect(navigate).toHaveBeenCalledWith('/admin')
  })
})
