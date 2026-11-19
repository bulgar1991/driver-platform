import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { finalize } from 'rxjs'
import { StaffMember, StaffRole } from '@/core/models'
import { AdminStaffService } from '@/core/services/admin-staff.service'
import { AuthService } from '@/core/services/auth.service'
import { problemMessage } from '@/core/utils/problem'
import { ToastService } from '@/core/services/toast.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import {
  DialogComponent,
  DialogOpenDirective,
  DialogService,
} from '@/shared/components/dialog'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

/** Panel accounts: admins and dispatchers. New accounts and resets get a temporary password shown once. */
@Component({
  selector: 'app-staff',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    MatSelect,
    MatOption,
    MatCheckbox,
    DialogComponent,
    DialogOpenDirective,
    ButtonComponent,
    IconComponent,
  ],
  templateUrl: './staff.component.html',
  styleUrl: './staff.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    editAccount: $localize`Edit account`,
    newAccount: $localize`New account`,
  }

  private readonly api = inject(AdminStaffService)
  private readonly fb = inject(FormBuilder).nonNullable
  private readonly dialogs = inject(DialogService)
  private readonly toast = inject(ToastService)
  protected readonly me = inject(AuthService).currentUser

  protected readonly staff = signal<StaffMember[]>([])
  protected readonly loading = signal(true)
  protected readonly message = signal<{ text: string; error: boolean } | null>(
    null
  )
  protected readonly editing = signal<StaffMember | null | undefined>(undefined)
  protected readonly saving = signal(false)
  /** The temporary password just created, with its owner. */
  protected readonly temporary = signal<{
    email: string
    password: string
  } | null>(null)

  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    fullName: ['', [Validators.required, Validators.maxLength(120)]],
    role: this.fb.control<StaffRole>('Dispatcher'),
    isActive: [true],
  })

  ngOnInit(): void {
    this.load()
  }

  protected open(member: StaffMember | null): void {
    this.message.set(null)
    this.form.reset({
      email: member?.email ?? '',
      fullName: member?.fullName ?? '',
      role: member?.role ?? 'Dispatcher',
      isActive: member?.isActive ?? true,
    })
    if (member) {
      this.form.controls.email.disable()
    } else {
      this.form.controls.email.enable()
    }
    // Nobody can deactivate their own account.
    if (member && member.id === this.me()?.id) {
      this.form.controls.isActive.disable()
    } else {
      this.form.controls.isActive.enable()
    }
    this.editing.set(member)
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    const member = this.editing()
    this.saving.set(true)
    if (member) {
      this.api
        .update(member.id, v.fullName.trim(), v.role, v.isActive)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.editing.set(undefined)
            this.notify($localize`${v.fullName} saved.`)
            this.load()
          },
          error: err => this.notify(problemMessage(err), true),
        })
    } else {
      this.api
        .create(v.email.trim(), v.fullName.trim(), v.role)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: r => {
            this.editing.set(undefined)
            this.temporary.set({
              email: r.user.email,
              password: r.temporaryPassword,
            })
            this.load()
          },
          error: err => this.notify(problemMessage(err), true),
        })
    }
  }

  protected reset(member: StaffMember): void {
    this.api.resetPassword(member.id).subscribe({
      next: r => {
        this.temporary.set({
          email: r.user.email,
          password: r.temporaryPassword,
        })
        this.load()
      },
      error: err => this.notify(problemMessage(err), true),
    })
  }

  /** Lost phone: they connect the authenticator again at the next sign-in. */
  protected resetTwoFactor(member: StaffMember): void {
    this.dialogs
      .confirm({
        title: $localize`Reset two-step sign-in`,
        message: $localize`Reset two-step sign-in for ${member.fullName}:name:? They will be signed out and must connect their authenticator app again.`,
        confirm: $localize`Reset`,
        danger: true,
      })
      .subscribe(ok => {
        if (ok) this.doResetTwoFactor(member)
      })
  }

  private doResetTwoFactor(member: StaffMember): void {
    this.api.resetTwoFactor(member.id).subscribe({
      next: () => {
        this.notify(
          $localize`Two-step sign-in reset for ${member.fullName}:name:.`
        )
        this.load()
      },
      error: err => this.notify(problemMessage(err), true),
    })
  }

  protected copy(value: string): void {
    void navigator.clipboard
      ?.writeText(value)
      .then(() => this.toast.success($localize`Copied.`))
  }

  private load(): void {
    this.loading.set(true)
    this.api
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: list => this.staff.set(list),
        error: err => this.notify(problemMessage(err), true),
      })
  }

  /** Confirmations come and go as a toast; errors stay on the page until the next action. */
  private notify(text: string, error = false): void {
    if (error) {
      this.message.set({ text, error })
    } else {
      this.message.set(null)
      this.toast.success(text)
    }
  }
}
