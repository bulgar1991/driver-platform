import { HttpErrorResponse } from '@angular/common/http'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, Router } from '@angular/router'
import { finalize } from 'rxjs'
import { AuthService } from '@/core/services/auth.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    FormFieldComponent,
    LogoComponent,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly auth = inject(AuthService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  })
  protected readonly submitting = signal(false)
  protected readonly error = signal<string | null>(null)

  protected fieldError(name: 'email' | 'password'): string | null {
    const control = this.form.controls[name]
    if (!control.touched || control.valid) {
      return null
    }
    if (control.hasError('required')) {
      return 'Required'
    }
    return control.hasError('email') ? 'Enter a valid email' : null
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    this.submitting.set(true)
    this.error.set(null)
    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          const returnUrl =
            this.route.snapshot.queryParamMap.get('returnUrl') ?? '/admin'
          void this.router.navigateByUrl(
            returnUrl.startsWith('/') ? returnUrl : '/admin'
          )
        },
        error: (err: unknown) => this.error.set(loginErrorMessage(err)),
      })
  }
}

function loginErrorMessage(err: unknown): string {
  const status = err instanceof HttpErrorResponse ? err.status : 0
  switch (status) {
    case 401:
      return 'Invalid email or password.'
    case 423:
      return 'Too many failed attempts. The account is locked for 15 minutes.'
    case 429:
      return 'Too many attempts. Wait a minute and try again.'
    default:
      return 'Could not reach the server. Try again.'
  }
}
