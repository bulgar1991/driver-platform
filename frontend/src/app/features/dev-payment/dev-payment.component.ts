import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core'
import { DevPaymentSession, PaymentMethod } from '@/core/models'
import { MyBookingService } from '@/core/services/my-booking.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'

/**
 * Stand-in for Stripe's page while the API runs with Payments:Provider = "Fake".
 * With Stripe configured the API answers 404 here and the client never sees this page.
 */
@Component({
  selector: 'app-dev-payment',
  imports: [ButtonComponent, MoneyPipe],
  templateUrl: './dev-payment.component.html',
  styleUrl: './dev-payment.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DevPaymentComponent {
  readonly sessionId = input.required<string>()

  private readonly api = inject(MyBookingService)
  protected readonly session = signal<DevPaymentSession | null>(null)
  protected readonly busy = signal(false)
  protected readonly error = signal<string | null>(null)

  constructor() {
    effect(() => {
      const id = this.sessionId()
      untracked(() =>
        this.api.devSession(id).subscribe({
          next: s => this.session.set(s),
          error: err =>
            this.error.set(problemMessage(err, 'Unknown payment session.')),
        })
      )
    })
  }

  protected pay(method: PaymentMethod): void {
    this.busy.set(true)
    this.api.devComplete(this.sessionId(), method).subscribe({
      next: url => window.location.assign(url),
      error: err => {
        this.busy.set(false)
        this.error.set(problemMessage(err))
      },
    })
  }

  protected cancel(): void {
    this.api.devCancel(this.sessionId()).subscribe({
      next: url => window.location.assign(url),
      error: err => this.error.set(problemMessage(err)),
    })
  }
}
