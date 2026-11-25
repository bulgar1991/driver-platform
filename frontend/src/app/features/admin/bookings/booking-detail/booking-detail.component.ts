import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { finalize, Observable } from 'rxjs'
import {
  BookingDetail,
  BookingStatus,
  isManualPayment,
  PaymentStatus,
} from '@/core/models'
import { AdminPaymentsService } from '@/core/services/admin-payments.service'
import { DialogService } from '@/shared/components/dialog/dialog.service'
import { ToastService } from '@/core/services/toast.service'
import { PAYMENT_METHOD_LABEL } from '@/core/utils/status-labels'
import { MoneyPipe as Money } from '@/shared/pipes/money.pipe'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { AuthService } from '@/core/services/auth.service'
import { ticketTime } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import { DurationPipe } from '@/shared/pipes/duration.pipe'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

const GROUP_LABEL = {
  Infant: $localize`infant seat`,
  Toddler: $localize`child seat`,
  Booster: $localize`booster`,
}

function flightForm(b: BookingDetail) {
  return {
    flightNumber: b.flightNumber ?? '',
    flightDepartsAt: b.flightDepartsAt?.slice(0, 16) ?? '',
    flightLandsAt: b.flightLandsAt?.slice(0, 16) ?? '',
  }
}

/** Everything about one booking, with the status changes a dispatcher may make. */
@Component({
  selector: 'app-booking-detail',
  imports: [
    MatFormField,
    MatLabel,
    MatInput,
    DatePipe,
    FormsModule,
    RouterLink,
    ButtonComponent,
    StatusBadgeComponent,
    DurationPipe,
    MoneyPipe,
  ],
  templateUrl: './booking-detail.component.html',
  styleUrl: './booking-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingDetailComponent {
  protected readonly text = ADMIN_TEXT
  readonly bookingId = input.required<string>()
  readonly changed = output<BookingDetail>()

  private readonly api = inject(AdminDispatchService)
  protected readonly isAdmin = inject(AuthService).hasRole('Admin')
  private readonly payments = inject(AdminPaymentsService)
  private readonly dialog = inject(DialogService)
  private readonly toast = inject(ToastService)
  protected readonly methodLabel = PAYMENT_METHOD_LABEL
  protected readonly paymentLabel: Record<PaymentStatus, string> = {
    Pending: $localize`to collect`,
    Succeeded: $localize`paid`,
    Failed: $localize`not completed`,
    Refunded: $localize`refunded`,
    PartiallyRefunded: $localize`partly refunded`,
  }

  /** A payment outside Stripe still waiting: the admin marks it received. */
  protected readonly canMarkReceived = computed(() => {
    const b = this.booking()
    return (
      this.isAdmin &&
      !!b?.paymentId &&
      b.paymentStatus === 'Pending' &&
      isManualPayment(b.paymentMethod)
    )
  })
  protected readonly booking = signal<BookingDetail | null>(null)
  protected readonly busy = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly cancelling = signal(false)
  protected cancelReason = ''
  protected notes = ''
  protected clientNotes = ''
  /** datetime-local values ("2026-11-02T14:30"), as on the ticket. */
  protected flight = {
    flightNumber: '',
    flightDepartsAt: '',
    flightLandsAt: '',
  }
  protected readonly groupLabel = GROUP_LABEL

  protected readonly actions = computed(() => {
    switch (this.booking()?.status) {
      case 'Assigned':
        return { pickedUp: true, completed: true, cancel: true }
      case 'PickedUp':
        return { pickedUp: false, completed: true, cancel: false }
      case 'New':
      case 'AwaitingPayment':
        return { pickedUp: false, completed: false, cancel: true }
      default:
        return { pickedUp: false, completed: false, cancel: false }
    }
  })

  constructor() {
    effect(() => {
      const id = this.bookingId()
      this.booking.set(null)
      this.cancelling.set(false)
      this.api.booking(id).subscribe({
        next: b => this.show(b),
        error: err => this.error.set(problemMessage(err)),
      })
    })
  }

  protected setStatus(status: BookingStatus): void {
    const reason = status === 'Cancelled' ? this.cancelReason.trim() : undefined
    if (status === 'Cancelled' && !reason) {
      this.error.set($localize`Say why the booking is cancelled.`)
      return
    }
    this.run(this.api.setBookingStatus(this.bookingId(), status, reason))
  }

  protected flightChanged(b: BookingDetail): boolean {
    const saved = flightForm(b)
    return (
      this.flight.flightNumber.trim() !== saved.flightNumber ||
      this.flight.flightDepartsAt !== saved.flightDepartsAt ||
      this.flight.flightLandsAt !== saved.flightLandsAt
    )
  }

  protected saveFlight(): void {
    const flightNumber = this.flight.flightNumber.trim() || null
    this.run(
      this.api.setBookingFlight(this.bookingId(), {
        flightNumber,
        flightDepartsAt: ticketTime(this.flight.flightDepartsAt),
        flightLandsAt: ticketTime(this.flight.flightLandsAt),
      })
    )
  }

  protected saveClientNotes(): void {
    this.run(this.api.setClientNotes(this.bookingId(), this.clientNotes))
  }

  protected saveNotes(): void {
    this.run(this.api.setBookingNotes(this.bookingId(), this.notes))
  }

  protected markReceived(b: BookingDetail): void {
    const amount = new Money().transform(b.totalPrice, b.currency)
    this.dialog
      .confirm({
        title: $localize`Payment received?`,
        message: $localize`${amount}:amount: for ${b.reference}:reference:. The invoice is issued and emailed to the client.`,
        confirm: $localize`Mark as received`,
      })
      .subscribe(ok => {
        if (!ok) return
        this.busy.set(true)
        this.payments
          .received(b.paymentId!)
          .pipe(finalize(() => this.busy.set(false)))
          .subscribe({
            next: () => {
              this.toast.success($localize`Payment received; invoice issued.`)
              this.run(this.api.booking(b.id))
            },
            error: err => this.error.set(problemMessage(err)),
          })
      })
  }

  private run(call: Observable<BookingDetail>): void {
    this.busy.set(true)
    this.error.set(null)
    call.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: b => {
        this.show(b)
        this.changed.emit(b)
      },
      error: err => this.error.set(problemMessage(err)),
    })
  }

  private show(b: BookingDetail): void {
    this.booking.set(b)
    this.notes = b.internalNotes ?? ''
    this.clientNotes = b.client.notesForDriver ?? ''
    this.flight = flightForm(b)
    this.cancelReason = ''
    this.cancelling.set(false)
  }
}
