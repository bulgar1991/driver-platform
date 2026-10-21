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
import { InvoiceSummary, PaymentListItem, PaymentStatus } from '@/core/models'
import { AdminPaymentsService } from '@/core/services/admin-payments.service'
import { saveBlob } from '@/core/utils/download'
import { todayInItaly } from '@/core/utils/italy-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'

const STATUSES: { value: PaymentStatus | null; label: string }[] = [
  { value: null, label: 'All' },
  { value: 'Succeeded', label: 'Paid' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Failed', label: 'Not completed' },
  { value: 'PartiallyRefunded', label: 'Partly refunded' },
  { value: 'Refunded', label: 'Refunded' },
]

const STATUS_LABEL: Record<PaymentStatus, string> = {
  Pending: 'Pending',
  Succeeded: 'Paid',
  Failed: 'Not completed',
  PartiallyRefunded: 'Partly refunded',
  Refunded: 'Refunded',
}

/** Business plan §5: payments, invoices, refunds with credit notes, export for the accountant. */
@Component({
  selector: 'app-payments',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    Dialog,
    ButtonComponent,
    FormFieldComponent,
    IconComponent,
    MoneyPipe,
  ],
  templateUrl: './payments.component.html',
  styleUrl: './payments.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentsComponent implements OnInit {
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
            `Refunded ${v.amount.toFixed(2)} € on ${updated.bookingReference}; the credit note was emailed.`
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
      next: () => this.notify(`${invoice.number} sent again.`),
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

  private notify(text: string, error = false): void {
    this.message.set({ text, error })
  }
}
