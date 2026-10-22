import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { RouterLink } from '@angular/router'
import { Dashboard } from '@/core/models'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { AuthService } from '@/core/services/auth.service'
import { poll } from '@/core/utils/poll'
import { problemMessage } from '@/core/utils/problem'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

interface Stat {
  label: string
  value: number
  link: string
  query?: Partial<Record<string, string>>
  alert?: boolean
}

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  protected readonly text = ADMIN_TEXT
  private readonly auth = inject(AuthService)
  protected readonly user = this.auth.currentUser
  protected readonly data = signal<Dashboard | null>(null)
  protected readonly error = signal<string | null>(null)
  private readonly api = inject(AdminDispatchService)

  protected readonly stats = computed<Stat[]>(() => {
    const d = this.data()
    if (!d) {
      return []
    }
    return [
      {
        label: $localize`To assign (next 48 h)`,
        value: d.unassignedNext48h,
        link: '/admin/bookings',
        query: { unassigned: '1' },
        alert: d.unassignedNext48h > 0,
      },
      {
        label: $localize`Rides today`,
        value: d.todayRides,
        link: '/admin/shifts',
      },
      {
        label: $localize`On the road`,
        value: d.inProgress,
        link: '/admin/bookings',
        query: { status: 'PickedUp' },
      },
      {
        label: $localize`Completed today`,
        value: d.todayCompleted,
        link: '/admin/bookings',
        query: { status: 'Completed' },
      },
      {
        label: $localize`Shifts to confirm`,
        value: d.shiftsToConfirm,
        link: '/admin/shifts',
        alert: d.shiftsToConfirm > 0,
      },
      {
        label: $localize`Awaiting payment`,
        value: d.awaitingPayment,
        link: '/admin/bookings',
        query: { status: 'AwaitingPayment' },
      },
    ]
  })

  constructor() {
    poll(
      () => this.api.dashboard(),
      undefined,
      err => this.error.set(problemMessage(err))
    )
      .pipe(takeUntilDestroyed())
      .subscribe(d => {
        this.error.set(null)
        this.data.set(d)
      })
  }
}
