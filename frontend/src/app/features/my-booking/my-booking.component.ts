import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core'
import { RouterLink } from '@angular/router'
import { finalize } from 'rxjs'
import { BookingStatus, MyBooking, PaymentStatus } from '@/core/models'
import { MyBookingService } from '@/core/services/my-booking.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { VEHICLE_INFO } from '../booking/vehicle-info'
import { InstructionsPipe } from '@/shared/pipes/instructions.pipe'
import { TranslatedPipe } from '@/shared/pipes/translated.pipe'

const STATUS_LABEL: Record<BookingStatus, string> = {
  AwaitingPayment: $localize`Awaiting payment`,
  New: $localize`Confirmed`,
  Assigned: $localize`Driver assigned`,
  PickedUp: $localize`On the way`,
  Completed: $localize`Completed`,
  Cancelled: $localize`Cancelled`,
}

const PAYMENT_LABEL: Record<PaymentStatus, string> = {
  Pending: $localize`Payment not completed`,
  Succeeded: $localize`Paid`,
  Failed: $localize`Payment not completed`,
  Refunded: $localize`Refunded`,
  PartiallyRefunded: $localize`Partly refunded`,
}

/** How long to wait for Stripe's webhook after the client comes back from the payment page. */
const POLL_EVERY_MS = 2000
const POLL_ATTEMPTS = 15

/** /my-booking/:reference/:token — the client's page from the signed link. */
@Component({
  selector: 'app-my-booking',
  imports: [
    DatePipe,
    RouterLink,
    ButtonComponent,
    IconComponent,
    InstructionsPipe,
    MoneyPipe,
    TranslatedPipe,
  ],
  templateUrl: './my-booking.component.html',
  styleUrl: './my-booking.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBookingComponent {
  // Route params and query params, bound by withComponentInputBinding.
  readonly reference = input.required<string>()
  readonly token = input.required<string>()
  readonly paid = input<string | undefined>(undefined)

  private readonly api = inject(MyBookingService)
  private readonly destroyRef = inject(DestroyRef)
  private pollTimer?: ReturnType<typeof setTimeout>

  protected readonly booking = signal<MyBooking | null>(null)
  protected readonly loading = signal(true)
  protected readonly paying = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly notFound = signal(false)
  protected readonly waitingForPayment = signal(false)

  protected readonly vehicles = VEHICLE_INFO
  protected readonly statusLabel = STATUS_LABEL
  protected readonly paymentLabel = PAYMENT_LABEL
  protected readonly t = {
    opening: $localize`Opening…`,
    pay: (amount: string) => $localize`Pay ${amount}:amount:`,
  }

  protected readonly paidOk = computed(() => {
    const status = this.booking()?.payment?.status
    return (
      status === 'Succeeded' ||
      status === 'PartiallyRefunded' ||
      status === 'Refunded'
    )
  })

  constructor() {
    effect(() => {
      const reference = this.reference()
      const token = this.token()
      const returning = this.paid() === '1'
      untracked(() =>
        this.load(reference, token, returning ? POLL_ATTEMPTS : 0)
      )
    })
    this.destroyRef.onDestroy(() => clearTimeout(this.pollTimer))
  }

  protected pay(): void {
    this.paying.set(true)
    this.error.set(null)
    this.api.checkout(this.reference(), this.token()).subscribe({
      next: url => window.location.assign(url),
      error: err => {
        this.paying.set(false)
        this.error.set(
          problemMessage(
            err,
            $localize`The payment page could not be opened. Please try again.`
          )
        )
      },
    })
  }

  protected invoiceUrl(number: string): string {
    return this.api.invoiceUrl(this.reference(), this.token(), number)
  }

  /** After Stripe, the webhook may arrive a moment later: poll until the booking is paid. */
  private load(reference: string, token: string, attemptsLeft: number): void {
    clearTimeout(this.pollTimer)
    this.api
      .get(reference, token)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: booking => {
          this.booking.set(booking)
          const waiting =
            booking.status === 'AwaitingPayment' && attemptsLeft > 0
          this.waitingForPayment.set(waiting)
          if (waiting) {
            this.pollTimer = setTimeout(
              () => this.load(reference, token, attemptsLeft - 1),
              POLL_EVERY_MS
            )
          }
        },
        error: err => {
          this.waitingForPayment.set(false)
          this.notFound.set((err as { status?: number }).status === 404)
          this.error.set(problemMessage(err))
        },
      })
  }
}
