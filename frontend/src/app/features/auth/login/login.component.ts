import { HttpErrorResponse } from '@angular/common/http'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, Router } from '@angular/router'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { finalize } from 'rxjs'
import { AuthService } from '@/core/services/auth.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'
import { LanguageSwitchComponent } from '@/layout/language-switch/language-switch.component'

@Component({
  selector: 'app-login',
  imports: [
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    ReactiveFormsModule,
    ButtonComponent,
    LanguageSwitchComponent,
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

  protected readonly tx = {
    enterCode: $localize`Enter the 6-digit code`,
  }

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  })
  protected readonly submitting = signal(false)
  protected readonly error = signal<string | null>(null)

  /** Second step: set once the password was right and two-step sign-in is on. */
  protected readonly challenge = signal<string | null>(null)
  protected readonly codeForm = inject(FormBuilder).nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6)]],
  })
  protected readonly code = this.codeForm.controls.code
  protected readonly notice = signal<string | null>(null)
  protected readonly sendingEmail = signal(false)

  protected fieldError(name: 'email' | 'password'): string | null {
    const control = this.form.controls[name]
    if (!control.touched || control.valid) {
      return null
    }
    if (control.hasError('required')) {
      return $localize`Required`
    }
    return control.hasError('email') ? $localize`Enter a valid email` : null
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
        next: result => {
          if ('twoFactorRequired' in result) {
            this.challenge.set(result.challengeToken)
            this.code.reset()
            this.notice.set(null)
            return
          }
          this.enter()
        },
        error: (err: unknown) => this.error.set(loginErrorMessage(err)),
      })
  }

  protected verify(): void {
    const challenge = this.challenge()
    if (!challenge || this.code.invalid) {
      this.code.markAsTouched()
      return
    }
    this.submitting.set(true)
    this.error.set(null)
    this.auth
      .verifyTwoFactor(challenge, this.code.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => this.enter(),
        error: (err: unknown) => {
          if (isExpired(err)) {
            this.back()
          }
          this.error.set(loginErrorMessage(err))
        },
      })
  }

  protected sendEmail(): void {
    const challenge = this.challenge()
    if (!challenge) {
      return
    }
    this.sendingEmail.set(true)
    this.error.set(null)
    this.auth
      .sendEmailCode(challenge)
      .pipe(finalize(() => this.sendingEmail.set(false)))
      .subscribe({
        next: () =>
          this.notice.set(
            $localize`If the email is not there within a minute, check the spam folder or use the app.`
          ),
        error: (err: unknown) => {
          if (isExpired(err)) {
            this.back()
          }
          this.error.set(loginErrorMessage(err))
        },
      })
  }

  /** Back to email and password (wrong account, or the 5 minutes ran out). */
  protected back(): void {
    this.challenge.set(null)
    this.notice.set(null)
    this.form.controls.password.reset()
  }

  private enter(): void {
    const returnUrl =
      this.route.snapshot.queryParamMap.get('returnUrl') ?? '/admin'
    void this.router.navigateByUrl(
      returnUrl.startsWith('/') ? returnUrl : '/admin'
    )
  }
}

function isExpired(err: unknown): boolean {
  return (
    err instanceof HttpErrorResponse &&
    (err.error as { type?: string } | null)?.type === 'two-factor-expired'
  )
}

function loginErrorMessage(err: unknown): string {
  const status = err instanceof HttpErrorResponse ? err.status : 0
  const type =
    err instanceof HttpErrorResponse
      ? (err.error as { type?: string } | null)?.type
      : undefined
  if (type === 'two-factor-code') {
    return $localize`The code is not correct. Use the newest code from the app.`
  }
  if (type === 'two-factor-expired') {
    return $localize`The 5 minutes for the code ran out. Sign in again.`
  }
  if (status === 409 && err instanceof HttpErrorResponse) {
    return problemMessage(err)
  }
  switch (status) {
    case 401:
      return $localize`Invalid email or password.`
    case 423:
      return $localize`Too many failed attempts. The account is locked for 15 minutes.`
    case 429:
      return $localize`Too many attempts. Wait a minute and try again.`
    default:
      return $localize`Could not reach the server. Try again.`
  }
}
