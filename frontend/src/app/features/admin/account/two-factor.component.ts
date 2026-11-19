import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Router } from '@angular/router'
import { finalize, Observable } from 'rxjs'
import { TwoFactorSetup, TwoFactorStatus } from '@/core/models'
import { AuthService } from '@/core/services/auth.service'
import { saveBlob } from '@/core/utils/download'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'

/**
 * My account → two-step sign-in: connect an authenticator app with a QR code, keep the recovery
 * codes, get new ones, or move to a new phone. Required for everyone before the panel opens.
 */
@Component({
  selector: 'app-two-factor',
  imports: [DatePipe, ReactiveFormsModule, ButtonComponent, FormFieldComponent],
  templateUrl: './two-factor.component.html',
  styleUrl: './two-factor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TwoFactorComponent implements OnInit {
  protected readonly tx = {
    enterCode: $localize`Enter the 6-digit code`,
    required: $localize`Required`,
  }

  private readonly auth = inject(AuthService)
  private readonly router = inject(Router)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly user = this.auth.currentUser
  protected readonly status = signal<TwoFactorStatus | null>(null)
  protected readonly setup = signal<TwoFactorSetup | null>(null)
  /** Shown once, right after turning it on or asking for new ones. */
  protected readonly recoveryCodes = signal<string[] | null>(null)
  /** "codes": ask for new recovery codes; "move": turn off to connect a new phone. */
  protected readonly action = signal<'codes' | 'move' | null>(null)
  protected readonly busy = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly notice = signal<string | null>(null)

  /** Set up because the panel required it: go on to the panel afterwards. */
  private forced = false

  protected readonly form = this.fb.group({
    code: [
      '',
      [Validators.required, Validators.pattern(/^\s*\d{3}\s?\d{3}\s*$/)],
    ],
    password: ['', Validators.required],
  })
  protected readonly code = this.form.controls.code
  protected readonly password = this.form.controls.password

  ngOnInit(): void {
    this.load()
  }

  protected start(): void {
    this.run(this.auth.setUpTwoFactor(), setup => {
      this.setup.set(setup)
      this.code.reset()
    })
  }

  protected confirm(): void {
    if (this.code.invalid) {
      this.code.markAsTouched()
      return
    }
    this.forced = this.user()?.twoFactorSetupRequired === true
    this.run(this.auth.enableTwoFactor(this.code.value), codes => {
      this.setup.set(null)
      this.recoveryCodes.set(codes)
      this.load()
    })
  }

  /** The codes are saved; after the required first setup, the panel opens. */
  protected codesSaved(): void {
    this.recoveryCodes.set(null)
    this.notice.set($localize`Two-step sign-in is on.`)
    if (this.forced) {
      this.forced = false
      void this.router.navigateByUrl('/admin')
    }
  }

  protected newCodes(): void {
    if (this.code.invalid) {
      this.code.markAsTouched()
      return
    }
    this.run(this.auth.newRecoveryCodes(this.code.value), codes => {
      this.action.set(null)
      this.recoveryCodes.set(codes)
      this.load()
    })
  }

  /** Turns it off (password + code) and, when it is required, shows a new QR code at once. */
  protected move(): void {
    if (this.code.invalid || this.password.invalid) {
      this.code.markAsTouched()
      this.password.markAsTouched()
      return
    }
    this.run(
      this.auth.disableTwoFactor(this.password.value, this.code.value),
      user => {
        this.action.set(null)
        this.password.reset()
        this.load()
        if (user.twoFactorSetupRequired) {
          this.start()
        } else {
          this.notice.set($localize`Two-step sign-in is off.`)
        }
      }
    )
  }

  protected choose(action: 'codes' | 'move' | null): void {
    this.action.set(action)
    this.code.reset()
    this.password.reset()
    this.error.set(null)
  }

  protected copy(codes: string[]): void {
    void navigator.clipboard?.writeText(codes.join('\n')).then(
      () => this.notice.set($localize`Copied.`),
      () => this.notice.set($localize`Select the codes and copy them.`)
    )
  }

  protected download(codes: string[]): void {
    const email = this.user()?.email ?? ''
    const text = [
      $localize`Recovery codes for ${email}:email:`,
      $localize`Each code works once, instead of the code from the app.`,
      '',
      ...codes,
    ].join('\n')
    saveBlob(new Blob([text], { type: 'text/plain' }), 'recovery-codes.txt')
  }

  private load(): void {
    this.auth.twoFactorStatus().subscribe({
      next: status => this.status.set(status),
      error: err => this.error.set(problemMessage(err)),
    })
  }

  private run<T>(request: Observable<T>, next: (value: T) => void): void {
    this.busy.set(true)
    this.error.set(null)
    this.notice.set(null)
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next,
      error: err => this.error.set(problemMessage(err)),
    })
  }
}
