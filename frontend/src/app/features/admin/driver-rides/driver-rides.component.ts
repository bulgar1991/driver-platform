import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core'
import { toObservable, toSignal } from '@angular/core/rxjs-interop'
import { RouterLink } from '@angular/router'
import { catchError, finalize, of, switchMap } from 'rxjs'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { todayInItaly } from '@/core/utils/italy-time'
import { problemMessage } from '@/core/utils/problem'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'

/** /admin/drivers/:id/rides — every ride on the driver's shifts, one month at a time. */
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
  /** Route param, bound by withComponentInputBinding. */
  readonly id = input.required<string>()

  private readonly api = inject(AdminFleetService)

  /** yyyy-MM, as the month input gives it. */
  protected readonly month = signal(todayInItaly().slice(0, 7))
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  protected readonly driver = toSignal(
    toObservable(this.id).pipe(
      switchMap(id => this.api.driver(id).pipe(catchError(() => of(null))))
    ),
    { initialValue: null }
  )

  protected readonly history = toSignal(
    toObservable(computed(() => ({ id: this.id(), month: this.month() }))).pipe(
      switchMap(({ id, month }) => {
        const [year, m] = month.split('-').map(Number)
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
    if (/^\d{4}-\d{2}$/.test(value)) {
      this.month.set(value)
    }
  }

  /** Previous (-1) or next (+1) month. */
  protected step(delta: number): void {
    const [year, m] = this.month().split('-').map(Number)
    const date = new Date(Date.UTC(year, m - 1 + delta, 1))
    this.month.set(date.toISOString().slice(0, 7))
  }
}
