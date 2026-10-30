import { DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core'
import { toObservable, toSignal } from '@angular/core/rxjs-interop'
import { RouterLink } from '@angular/router'
import { catchError, finalize, of, switchMap } from 'rxjs'
import { DriverPayLine } from '@/core/models'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import {
  addMonths,
  isMonth,
  splitMonth,
  thisMonthInItaly,
} from '@/core/utils/month'
import { problemMessage } from '@/core/utils/problem'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'

/** /admin/driver-pay — what each driver earned in a month, from the pay typed on their completed rides. */
@Component({
  selector: 'app-driver-pay',
  imports: [DecimalPipe, RouterLink, IconComponent, MoneyPipe],
  templateUrl: './driver-pay.component.html',
  styleUrl: './driver-pay.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverPayComponent {
  private readonly api = inject(AdminFleetService)

  protected readonly month = signal(thisMonthInItaly())
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  protected readonly lines = toSignal(
    toObservable(this.month).pipe(
      switchMap(month => {
        const [year, m] = splitMonth(month)
        this.loading.set(true)
        this.error.set(null)
        return this.api.driverPay(year, m).pipe(
          catchError(err => {
            this.error.set(problemMessage(err))
            return of<DriverPayLine[]>([])
          }),
          finalize(() => this.loading.set(false))
        )
      })
    ),
    { initialValue: [] as DriverPayLine[] }
  )

  protected readonly total = computed(() =>
    this.lines().reduce((sum, l) => sum + l.payTotal, 0)
  )
  protected readonly missing = computed(() =>
    this.lines().reduce((sum, l) => sum + l.ridesWithoutPay, 0)
  )

  protected setMonth(value: string): void {
    if (isMonth(value)) {
      this.month.set(value)
    }
  }

  protected step(delta: number): void {
    this.month.set(addMonths(this.month(), delta))
  }
}
