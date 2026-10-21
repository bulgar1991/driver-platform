import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Dialog } from 'primeng/dialog'
import { finalize } from 'rxjs'
import { StaffMember, StaffRole } from '@/core/models'
import { AdminStaffService } from '@/core/services/admin-staff.service'
import { AuthService } from '@/core/services/auth.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'

/** Panel accounts: admins and dispatchers. New accounts and resets get a temporary password shown once. */
@Component({
  selector: 'app-staff',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    Dialog,
    ButtonComponent,
    FormFieldComponent,
    IconComponent,
  ],
  templateUrl: './staff.component.html',
  styleUrl: './staff.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffComponent implements OnInit {
  private readonly api = inject(AdminStaffService)
  private readonly fb = inject(FormBuilder).nonNullable
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
            this.notify(`${v.fullName} saved.`)
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

  protected copy(value: string): void {
    void navigator.clipboard?.writeText(value)
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

  private notify(text: string, error = false): void {
    this.message.set({ text, error })
  }
}
