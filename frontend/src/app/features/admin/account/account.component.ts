import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core'
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms'
import { Router } from '@angular/router'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { ErrorStateMatcher } from '@angular/material/core'
import { finalize } from 'rxjs'
import { AuthService } from '@/core/services/auth.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { TwoFactorComponent } from './two-factor.component'

function sameAsNew(group: AbstractControl): ValidationErrors | null {
  const { newPassword, confirm } = group.value as {
    newPassword: string
    confirm: string
  }
  return confirm && newPassword !== confirm ? { mismatch: true } : null
}

/** /admin/account — change your own password; forced while it is a temporary one. */
@Component({
  selector: 'app-account',
  imports: [
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    ReactiveFormsModule,
    ButtonComponent,
    TwoFactorComponent,
  ],
  templateUrl: './account.component.html',
  styleUrl: './account.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountComponent {
  /** "Repeat the new password" turns red when the two passwords differ (an error of the form). */
  protected readonly mismatchMatcher: ErrorStateMatcher = {
    isErrorState: control =>
      !!control?.touched && !!control.parent?.hasError('mismatch'),
  }
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    minLength: $localize`At least 12 characters`,
    passwordsDiffer: $localize`The two passwords differ`,
  }

  private readonly auth = inject(AuthService)
  private readonly router = inject(Router)

  protected readonly user = this.auth.currentUser
  protected readonly saving = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly done = signal(false)

  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(12)]],
      confirm: ['', Validators.required],
    },
    { validators: sameAsNew }
  )

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const forced = this.user()?.mustChangePassword
    const { currentPassword, newPassword } = this.form.getRawValue()
    this.saving.set(true)
    this.error.set(null)
    this.auth
      .changePassword({ currentPassword, newPassword })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.form.reset()
          this.done.set(true)
          if (forced) {
            void this.router.navigateByUrl('/admin')
          }
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
