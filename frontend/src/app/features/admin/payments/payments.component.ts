import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatCheckbox } from '@angular/material/checkbox'
import {
  DialogComponent,
  DialogOpenDirective,
} from '@/shared/components/dialog'
import { finalize } from 'rxjs'
import { InvoiceSummary, PaymentListItem, PaymentStatus } from '@/core/models'
import { AdminPaymentsService } from '@/core/services/admin-payments.service'
import { ToastService } from '@/core/services/toast.service'
import { saveBlob } from '@/core/utils/download'
import { todayInItaly } from '@/core/utils/italy-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

const STATUSES: { value: PaymentStatus | null; label: string }[] = [
  { value: null, label: $localize`All` },
  { value: 'Succeeded', label: $localize`Paid` },
  { value: 'Pending', label: $localize`Pending` },
  { value: 'Failed', label: $localize`Not completed` },
  { value: 'PartiallyRefunded', label: $localize`Partly refunded` },
  { value: 'Refunded', label: $localize`Refunded` },
]

const STATUS_LABEL: Record<PaymentStatus, string> = {
  Pending: $localize`Pending`,
  Succeeded: $localize`Paid`,
  Failed: $localize`Not completed`,
  PartiallyRefunded: $localize`Partly refunded`,
  Refunded: $localize`Refunded`,
}

/** Business plan §5: payments, invoices, refunds with credit notes, export for the accountant. */
@Component({
  selector: 'app-payments',
  imports: [
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    MatCheckbox,
    DialogComponent,
    DialogOpenDirective,
    DatePipe,
    ReactiveFormsModule,
    ButtonComponent,
    IconComponent,
    MoneyPipe,
  ],
  templateUrl: './payments.component.html',
  styleUrl: './payments.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentsComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    emailAgain: (invoice: string) => $localize`Email ${invoice}:invoice: again`,
    refundLimit: $localize`Up to what is left to refund`,
  }

  private readonly toast = inject(ToastService)
  private readonly api = inject(AdminPaymentsService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly statuses = STATUSES
  protected readonly statusLabel = STATUS_LABEL
  private readonly money = new MoneyPipe()
  protected readonly status = signal<PaymentStatus | null>(null)
  protected readonly payments = signal<PaymentListItem[]>([])
  protected readonly loading = signal(true)
  protected readonly message = signal<{ text: string; error: boolean } | null>(
    null
  )
  protected readonly refunding = signal<PaymentListItem | null>(null)
  protected readonly saving = signal(false)

  protected readonly refundForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    reason: ['', [Validators.required, Validators.maxLength(300)]],
    cancelBooking: [false],
  })

  protected readonly exportForm = this.fb.group({
    from: [todayInItaly().slice(0, 8) + '01', Validators.required],
    to: [todayInItaly(), Validators.required],
  })

  ngOnInit(): void {
    this.load()
  }

  protected filter(status: PaymentStatus | null): void {
    this.status.set(status)
    this.load()
  }

  protected canRefund(p: PaymentListItem): boolean {
    return (
      (p.status === 'Succeeded' || p.status === 'PartiallyRefunded') &&
      p.refundedAmount < p.amount
    )
  }

  protected openRefund(p: PaymentListItem): void {
    const left = Math.round((p.amount - p.refundedAmount) * 100) / 100
    this.refundForm.reset({ amount: left, reason: '', cancelBooking: false })
    this.refundForm.controls.amount.setValidators([
      Validators.required,
      Validators.min(0.01),
      Validators.max(left),
    ])
    this.refundForm.controls.amount.updateValueAndValidity()
    this.refunding.set(p)
  }

  protected refund(): void {
    const payment = this.refunding()
    if (!payment || this.refundForm.invalid) {
      this.refundForm.markAllAsTouched()
      return
    }
    const v = this.refundForm.getRawValue()
    this.saving.set(true)
    this.api
      .refund(payment.id, {
        amount: v.amount,
        reason: v.reason.trim(),
        cancelBooking: v.cancelBooking,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: updated => {
          this.payments.update(list =>
            list.map(p => (p.id === updated.id ? updated : p))
          )
          this.refunding.set(null)
          this.notify(
            $localize`Refunded ${v.amount.toFixed(2)} € on ${updated.bookingReference}; the credit note was emailed.`
          )
        },
        error: err => this.notify(problemMessage(err), true),
      })
  }

  protected download(invoice: InvoiceSummary): void {
    this.api.invoicePdf(invoice.id).subscribe({
      next: blob => saveBlob(blob, `${invoice.number}.pdf`),
      error: err => this.notify(problemMessage(err), true),
    })
  }

  protected resend(invoice: InvoiceSummary): void {
    this.api.resend(invoice.id).subscribe({
      next: () => this.notify($localize`${invoice.number} sent again.`),
      error: err => this.notify(problemMessage(err), true),
    })
  }

  protected exportCsv(): void {
    const { from, to } = this.exportForm.getRawValue()
    this.api.export(from, to).subscribe({
      next: blob => saveBlob(blob, `invoices-${from}-${to}.csv`),
      error: err => this.notify(problemMessage(err), true),
    })
  }

  private load(): void {
    this.loading.set(true)
    this.api
      .list(this.status())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: list => this.payments.set(list),
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
