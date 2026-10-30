import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core'
import { toObservable, toSignal } from '@angular/core/rxjs-interop'
import { RouterLink } from '@angular/router'
import { catchError, finalize, of, switchMap } from 'rxjs'
import { DriverRide } from '@/core/models'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { AuthService } from '@/core/services/auth.service'
import {
  addMonths,
  isMonth,
  splitMonth,
  thisMonthInItaly,
} from '@/core/utils/month'
import { problemMessage } from '@/core/utils/problem'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'

/** /admin/drivers/:id/rides — every ride on the driver's shifts, one month at a time. Admins also type the pay. */
@Component({
  selector: 'app-driver-rides',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    IconComponent,
    StatusBadgeComponent,
    MoneyPipe,
  ],
  templateUrl: './driver-rides.component.html',
  styleUrl: './driver-rides.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverRidesComponent {
  // Route param and ?month=yyyy-MM, bound by withComponentInputBinding.
  readonly id = input.required<string>()
  readonly month = input<string | undefined>(undefined)

  protected readonly tx = {
    pay: (reference: string) =>
      $localize`Driver pay for ${reference}:reference:`,
  }

  private readonly api = inject(AdminFleetService)
  protected readonly isAdmin = inject(AuthService).hasRole('Admin')

  /** yyyy-MM, as the month input gives it. */
  protected readonly selectedMonth = linkedSignal(() => {
    const param = this.month()
    return isMonth(param) ? param : thisMonthInItaly()
  })
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  /** Booking id → message, while saving or after a failed save. */
  protected readonly payState = signal<Record<string, string>>({})
  /** Bumped after a pay change so the totals are read again. */
  private readonly reload = signal(0)

  protected readonly driver = toSignal(
    toObservable(this.id).pipe(
      switchMap(id => this.api.driver(id).pipe(catchError(() => of(null))))
    ),
    { initialValue: null }
  )

  protected readonly history = toSignal(
    toObservable(
      computed(() => ({
        id: this.id(),
        month: this.selectedMonth(),
        reload: this.reload(),
      }))
    ).pipe(
      switchMap(({ id, month }) => {
        const [year, m] = splitMonth(month)
        this.loading.set(true)
        this.error.set(null)
        return this.api.driverRides(id, year, m).pipe(
          catchError(err => {
            this.error.set(problemMessage(err))
            return of(null)
          }),
          finalize(() => this.loading.set(false))
        )
      })
    ),
    { initialValue: null }
  )

  protected setMonth(value: string): void {
    if (isMonth(value)) {
      this.selectedMonth.set(value)
    }
  }

  /** Previous (-1) or next (+1) month. */
  protected step(delta: number): void {
    this.selectedMonth.set(addMonths(this.selectedMonth(), delta))
  }

  /** Saved when the field loses focus or Enter is pressed; an empty field clears the pay. */
  protected savePay(ride: DriverRide, value: string): void {
    const amount = value.trim() === '' ? null : Number(value)
    if (amount === ride.driverPay) {
      return
    }
    if (amount !== null && (!Number.isFinite(amount) || amount < 0)) {
      this.setPayState(ride.bookingId, $localize`Enter an amount of 0 or more.`)
      return
    }
    this.setPayState(ride.bookingId, $localize`Saving…`)
    this.api.setDriverPay(ride.bookingId, amount).subscribe({
      next: () => {
        this.setPayState(ride.bookingId, null)
        this.reload.update(n => n + 1)
      },
      error: err => this.setPayState(ride.bookingId, problemMessage(err)),
    })
  }

  private setPayState(bookingId: string, message: string | null): void {
    this.payState.update(state => {
      const next = { ...state }
      if (message) {
        next[bookingId] = message
      } else {
        delete next[bookingId]
      }
      return next
    })
  }
}
