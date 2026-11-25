import { ChangeDetectionStrategy, Component, inject } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog'
import {
  MatError,
  MatFormField,
  MatHint,
  MatLabel,
} from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { OfficeCustomerInput } from '@/core/models'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { DialogComponent } from '@/shared/components/dialog/dialog.component'

export interface CustomerRecordData {
  /** What was typed in "Find a customer", or the record being edited. */
  search: string
  value: OfficeCustomerInput | null
}

const LANGUAGES = [
  { code: 'it', label: 'Italiano' },
  { code: 'en', label: 'English' },
  { code: 'de', label: 'Deutsch' },
  { code: 'fr', label: 'Français' },
]

/**
 * New booking → a customer not found in the search is registered here before the booking is saved:
 * name and phone are required, the rest (company, tax and billing details) when the office has them.
 */
@Component({
  selector: 'app-customer-record-dialog',
  imports: [
    DialogComponent,
    ButtonComponent,
    ReactiveFormsModule,
    MatFormField,
    MatLabel,
    MatHint,
    MatError,
    MatInput,
    MatSelect,
    MatOption,
  ],
  templateUrl: './customer-record-dialog.component.html',
  styleUrl: './customer-record-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerRecordDialogComponent {
  private readonly data = inject<CustomerRecordData>(MAT_DIALOG_DATA)
  protected readonly ref = inject(MatDialogRef<CustomerRecordDialogComponent>)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly languages = LANGUAGES
  protected readonly required = $localize`Required.`
  protected readonly validEmail = $localize`Enter a valid email.`

  protected readonly form = this.fb.group({
    company: ['', Validators.maxLength(150)],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', Validators.maxLength(100)],
    phone: ['', [Validators.required, Validators.maxLength(32)]],
    email: ['', [Validators.email, Validators.maxLength(254)]],
    language: ['it'],
    taxCode: ['', Validators.maxLength(32)],
    vatNumber: ['', Validators.maxLength(32)],
    recipientCode: ['', Validators.pattern(/^[A-Za-z0-9]{6,7}$/)],
    pecEmail: ['', [Validators.email, Validators.maxLength(254)]],
    billingEmail: ['', [Validators.email, Validators.maxLength(254)]],
    address: ['', Validators.maxLength(200)],
    postalCode: ['', Validators.maxLength(16)],
    city: ['', Validators.maxLength(100)],
    province: ['', Validators.maxLength(50)],
    country: ['', Validators.pattern(/^[A-Za-z]{2}$/)],
    notes: ['', Validators.maxLength(1000)],
  })

  constructor() {
    const v = this.data.value
    if (v) {
      this.form.patchValue(
        Object.fromEntries(
          Object.entries(v).map(([k, x]) => [k, x ?? ''])
        ) as Partial<Record<keyof OfficeCustomerInput, string>>
      )
      return
    }
    // What was searched for goes to the field it looks like.
    const search = this.data.search.trim()
    if (/@/.test(search)) {
      this.form.patchValue({ email: search })
    } else if (/^\+?[\d\s()./-]{6,}$/.test(search)) {
      this.form.patchValue({ phone: search })
    } else if (search) {
      const [first, ...rest] = search.split(/\s+/)
      this.form.patchValue({ firstName: first, lastName: rest.join(' ') })
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    const text = (s: string) => s.trim() || null
    const result: OfficeCustomerInput = {
      firstName: v.firstName.trim(),
      lastName: v.lastName.trim(),
      phone: v.phone.trim(),
      email: text(v.email),
      language: v.language,
      company: text(v.company),
      taxCode: text(v.taxCode),
      vatNumber: text(v.vatNumber),
      recipientCode: text(v.recipientCode),
      pecEmail: text(v.pecEmail),
      billingEmail: text(v.billingEmail),
      address: text(v.address),
      postalCode: text(v.postalCode),
      city: text(v.city),
      province: text(v.province),
      country: text(v.country)?.toUpperCase() ?? null,
      notes: text(v.notes),
    }
    this.ref.close(result)
  }
}
